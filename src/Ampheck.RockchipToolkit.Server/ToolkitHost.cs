using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Ampheck.RockchipToolkit.Server.Adb;
using Ampheck.RockchipToolkit.Server.Blobs;
using Ampheck.RockchipToolkit.Server.Devices;
using Ampheck.RockchipToolkit.Server.Generated;
using Ampheck.RockchipToolkit.Server.Serial;
using Ampheck.RockchipToolkit.Server.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.Server;

public sealed partial class ToolkitHost : IAsyncDisposable {
    private const string RpcProtocol = "arkt.rpc.v1";
    private const string SerialProtocol = "arkt.serial.v1";
    private const string AdbProtocol = "arkt.adb.v1";
    private readonly ToolkitHostOptions _options;
    private readonly ILogger<ToolkitHost> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ServiceProvider _services;
    private readonly BlobStore _blobs;
    private readonly TaskManager _tasks;
    private readonly SerialTerminalRegistry _serialTerminals;
    private readonly AdbToolkitService _adb;
    private readonly ToolkitDiscoveryResponder _discovery;
    private readonly ToolkitBroadcastResponder _broadcast;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<long, Task> _clients = [];
    private long _activeClients;
    private long _nextClientId;
    private long _lastActivityTimestamp =
        DateTimeOffset.UtcNow.UtcTicks;
    private int _disposed;

    public ToolkitHost(
        ToolkitHostOptions options,
        ILoggerFactory? loggerFactory = null,
        ILogger<ToolkitHost>? logger = null) {
        _options = options ??
            throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);
        _loggerFactory = loggerFactory ??
            NullLoggerFactory.Instance;
        _logger = logger ??
            _loggerFactory.CreateLogger<ToolkitHost>();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(options.TransferLimits);
        services.AddSingleton(_loggerFactory);
        services.AddSingleton(_ =>
            new BlobStore(
                options.TransferLimits.MaximumBlobBytes));
        services.AddSingleton<TaskManager>();
        services.AddSingleton<DeviceOperationGate>();
        services.AddScoped<DeviceAccess>();
        services.AddSingleton(_ =>
            new ToolkitSerialService(
                _loggerFactory,
                options.SerialHistoryBytes));
        services.AddSingleton<SerialTerminalRegistry>();
        services.AddSingleton(_ =>
            new AdbServerProcess(
                options.Endpoint.Host,
                options.AdbPort,
                _loggerFactory));
        services.AddGeneratedToolkitServices();
        _services = services.BuildServiceProvider();
        _blobs = _services.GetRequiredService<BlobStore>();
        _tasks = _services.GetRequiredService<TaskManager>();
        _serialTerminals =
            _services.GetRequiredService<SerialTerminalRegistry>();
        _adb = _services.GetRequiredService<AdbToolkitService>();
        _discovery = new ToolkitDiscoveryResponder(
            options,
            _logger);
        _broadcast = new ToolkitBroadcastResponder(options);
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        ServerAccessDiagnostics.Report(_logger);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdown.Token);
        var address = _options.Endpoint.Host.Equals(
            "localhost",
            StringComparison.OrdinalIgnoreCase)
            ? IPAddress.Loopback
            : IPAddress.TryParse(
                _options.Endpoint.Host,
                out var parsedAddress)
                ? parsedAddress
                : null;
        if (address is null) {
            throw new ArgumentException(
                $"Invalid server listen address '{_options.Endpoint.Host}'.");
        }

        var listener = new TcpListener(
            address,
            _options.Endpoint.Port);
        listener.Start();
        _logger.LogInformation(
            "Toolkit server listening on {Endpoint}",
            _options.Endpoint);
        TryStartDiscovery(
            _discovery.Start,
            "LAN discovery");
        TryStartDiscovery(
            _broadcast.Start,
            "broadcast discovery");
        var monitorTask = MonitorIdleAsync(stop.Token);
        try {
            _options.OnListening?.Invoke();
            while (!stop.IsCancellationRequested) {
                TcpClient client;
                try {
                    client = await listener.AcceptTcpClientAsync(
                            stop.Token)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) when (
                    stop.IsCancellationRequested) {
                    break;
                }

                var clientId = Interlocked.Increment(
                    ref _nextClientId);
                var clientTask = HandleClientAsync(
                    client,
                    stop.Token);
                _clients[clientId] = clientTask;
                _ = clientTask.ContinueWith(
                    completed => _clients.TryRemove(
                        clientId,
                        out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        } finally {
            await stop.CancelAsync().ConfigureAwait(false);
            listener.Stop();
            await Task.WhenAll(_clients.Values)
                .ConfigureAwait(false);
            await monitorTask.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        await _shutdown.CancelAsync().ConfigureAwait(false);
        Exception? failure = null;
        async ValueTask CleanupAsync(Func<ValueTask> dispose) {
            try {
                await dispose().ConfigureAwait(false);
            } catch (Exception error) {
                failure ??= error;
            }
        }

        await CleanupAsync(_discovery.DisposeAsync)
            .ConfigureAwait(false);
        await CleanupAsync(_broadcast.DisposeAsync)
            .ConfigureAwait(false);
        try {
            await _services.DisposeAsync().ConfigureAwait(false);
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _shutdown.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void TryStartDiscovery(
        Action start,
        string description) {
        try {
            start();
        } catch (Exception error) when (
            error is SocketException or UnauthorizedAccessException) {
            _logger.LogWarning(
                "{Description} is unavailable: {ErrorMessage}",
                description,
                error.Message);
        }
    }

    private async Task MonitorIdleAsync(
        CancellationToken serverToken) {
        if (_options.IdleTimeout == Timeout.InfiniteTimeSpan) {
            return;
        }

        var interval = TimeSpan.FromMilliseconds(
            Math.Clamp(
                _options.IdleTimeout.TotalMilliseconds / 4,
                100,
                1000));
        try {
            while (!serverToken.IsCancellationRequested) {
                await Task.Delay(interval, serverToken)
                    .ConfigureAwait(false);
                if (Volatile.Read(ref _activeClients) != 0) {
                    continue;
                }

                var lastActivity = new DateTimeOffset(
                    Interlocked.Read(ref _lastActivityTimestamp),
                    TimeSpan.Zero);
                if (DateTimeOffset.UtcNow - lastActivity <
                    _options.IdleTimeout) {
                    continue;
                }

                _logger.LogInformation(
                    "Toolkit server stopped after being idle for " +
                    "{IdleTimeout}",
                    _options.IdleTimeout);
                await _shutdown.CancelAsync().ConfigureAwait(false);
                return;
            }
        } catch (OperationCanceledException) when (
            serverToken.IsCancellationRequested) {
        }
    }

    private static void ValidateOptions(ToolkitHostOptions options) {
        ArgumentNullException.ThrowIfNull(options.Endpoint);
        if (options.Endpoint.Port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The toolkit server port is invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.InstanceId) ||
            string.IsNullOrWhiteSpace(options.HostName)) {
            throw new ArgumentException(
                "The toolkit server identity is invalid.");
        }

        if (options.HostName.Length > 255 ||
            options.HostName.Any(char.IsControl)) {
            throw new ArgumentException(
                "The toolkit server hostname must be 255 printable " +
                "characters or fewer.");
        }

        if (options.IdleTimeout != Timeout.InfiniteTimeSpan &&
            options.IdleTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The toolkit server idle timeout is invalid.");
        }

        options.TransferLimits.Validate();
        if (options.SerialHistoryBytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The serial history size must be positive.");
        }

        if (options.AdbPort is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The ADB server port is invalid.");
        }
    }
}
