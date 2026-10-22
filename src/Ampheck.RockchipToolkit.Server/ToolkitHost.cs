using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Ampheck.Adb.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.Server;

public sealed class ToolkitHost : IAsyncDisposable {
    private readonly ToolkitHostOptions _options;
    private readonly IToolkitExecutor _executor;
    private readonly ILogger<ToolkitHost> _logger;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ToolkitDiscoveryResponder _discovery;
    private readonly ToolkitBroadcastResponder _broadcast;
    private readonly ToolkitSerialService _serial;
    private readonly AdbServerDaemon _adb;
    private readonly ConcurrentDictionary<long, Task> _clients = [];
    private long _activeClients;
    private long _nextClientId;
    private long _lastActivityTimestamp =
        DateTimeOffset.UtcNow.UtcTicks;
    private int _disposed;

    public ToolkitHost(
        ToolkitHostOptions options,
        IToolkitExecutor executor,
        ILoggerFactory? loggerFactory = null,
        ILogger<ToolkitHost>? logger = null) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ??
            loggerFactory?.CreateLogger<ToolkitHost>() ??
            NullLogger<ToolkitHost>.Instance;
        ValidateOptions(options);
        _serial = new ToolkitSerialService(
            loggerFactory ?? NullLoggerFactory.Instance,
            options.SerialHistoryBytes);
        _adb = new AdbServerDaemon(
            new AdbServerDaemonOptions {
                Host = options.Endpoint.Host,
                Port = options.AdbPort
            },
            loggerFactory ?? NullLoggerFactory.Instance);
        _discovery = new ToolkitDiscoveryResponder(options, _logger);
        _broadcast = new ToolkitBroadcastResponder(options);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
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

        var listener = new TcpListener(address, _options.Endpoint.Port);
        listener.Start();
        _logger.LogInformation("Toolkit server listening on {Endpoint}", _options.Endpoint);
        try {
            _discovery.Start();
        } catch (Exception error) when (error is SocketException or UnauthorizedAccessException) {
            _logger.LogWarning(
                "Toolkit LAN discovery is unavailable: {ErrorMessage}",
                error.Message);
        }

        try {
            _broadcast.Start();
        } catch (Exception error) when (
            error is SocketException or UnauthorizedAccessException) {
            _logger.LogWarning(
                "Toolkit broadcast discovery is unavailable: {ErrorMessage}",
                error.Message);
        }

        var monitorTask = MonitorIdleAsync(stop.Token);
        var adbTask = RunAdbAsync(stop.Token);
        try {
            while (!stop.IsCancellationRequested) {
                TcpClient client;
                try {
                    client = await listener.AcceptTcpClientAsync(stop.Token)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
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
            listener.Stop();
            await Task.WhenAll(_clients.Values)
                .ConfigureAwait(false);
            await Task.WhenAll(monitorTask, adbTask)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        _shutdown.Cancel();
        await _discovery.DisposeAsync().ConfigureAwait(false);
        await _broadcast.DisposeAsync().ConfigureAwait(false);
        await _serial.DisposeAsync().ConfigureAwait(false);
        await _adb.DisposeAsync().ConfigureAwait(false);
        _operationGate.Dispose();
        _shutdown.Dispose();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverToken) {
        Interlocked.Increment(ref _activeClients);
        client.NoDelay = true;
        try {
            var stream = client.GetStream();
            using var socket = await ToolkitWebSocketHandshake.AcceptAsync(stream, serverToken)
                .ConfigureAwait(false);
            await using var connection = new ToolkitConnection(
                socket,
                _options,
                _executor,
                _serial,
                _operationGate,
                _logger,
                client.Client.RemoteEndPoint?.ToString(),
                serverToken);
            await connection.RunAsync().ConfigureAwait(false);
        } catch (EndOfStreamException) {
        } catch (Exception error) when (error is IOException or SocketException or
            OperationCanceledException or InvalidDataException or System.Net.WebSockets.WebSocketException) {
            _logger.LogDebug(
                "Toolkit WebSocket client disconnected: {ErrorMessage}",
                error.Message);
        } catch (Exception error) {
            _logger.LogError(
                error,
                "Toolkit WebSocket client handler failed");
        } finally {
            client.Dispose();
            Interlocked.Decrement(ref _activeClients);
            Interlocked.Exchange(ref _lastActivityTimestamp, DateTimeOffset.UtcNow.UtcTicks);
        }
    }

    private async Task MonitorIdleAsync(CancellationToken serverToken) {
        if (_options.IdleTimeout == Timeout.InfiniteTimeSpan) {
            return;
        }

        var interval = TimeSpan.FromMilliseconds(Math.Clamp(_options.IdleTimeout.TotalMilliseconds / 4, 100, 1000));
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

    private async Task RunAdbAsync(
        CancellationToken serverToken) {
        try {
            await _adb.RunAsync(serverToken).ConfigureAwait(false);
        } catch (Exception error) when (
            error is SocketException or
            InvalidOperationException or
            ArgumentException or
            OperationCanceledException) {
            if (!serverToken.IsCancellationRequested) {
                _logger.LogWarning(
                    error,
                    "Integrated ADB server is unavailable on port {Port}",
                    _options.AdbPort);
            }
        }
    }

    private static void ValidateOptions(ToolkitHostOptions options) {
        ArgumentNullException.ThrowIfNull(options.Endpoint);
        if (options.Endpoint.Port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(options), "The toolkit server port is invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.InstanceId) ||
            string.IsNullOrWhiteSpace(options.HostName)) {
            throw new ArgumentException("The toolkit server identity is invalid.");
        }

        if (options.HostName.Length > 255 ||
            options.HostName.Any(char.IsControl)) {
            throw new ArgumentException(
                "The toolkit server hostname must be 255 printable " +
                "characters or fewer.");
        }

        if (options.IdleTimeout != Timeout.InfiniteTimeSpan && options.IdleTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(options), "The toolkit server idle timeout is invalid.");
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
                "The integrated ADB server port is invalid.");
        }
    }
}
