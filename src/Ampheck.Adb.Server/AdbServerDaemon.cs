using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.Adb.Server;

public sealed class AdbServerDaemon : IAsyncDisposable {
    private readonly AdbServerDaemonOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AdbServerDaemon> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly AdbServerDeviceRegistry _devices;
    private readonly AdbServerForwardManager _forwards;
    private TcpListener? _listener;
    private bool _disposed;

    public AdbServerDaemon(
        AdbServerDaemonOptions? options = null,
        ILoggerFactory? loggerFactory = null) {
        _options = options ?? new AdbServerDaemonOptions();
        ValidateOptions(_options);
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<AdbServerDaemon>();
        _devices = new AdbServerDeviceRegistry(
            _options.Usb,
            _loggerFactory);
        _forwards = new AdbServerForwardManager(
            _devices,
            _loggerFactory.CreateLogger<AdbServerForwardManager>());
    }

    public async Task RunAsync(
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdown.Token);
        _listener = CreateListener(_options);
        _listener.Start();
        _logger.LogInformation(
            "ADB server listening on {Host}:{Port}",
            _options.Host,
            _options.Port);
        try {
            await _devices.RefreshAsync(stop.Token)
                .ConfigureAwait(false);
            while (!stop.IsCancellationRequested) {
                TcpClient client;
                try {
                    client = await _listener.AcceptTcpClientAsync(
                            stop.Token)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) when (
                    stop.IsCancellationRequested) {
                    break;
                }

                _ = HandleClientAsync(client, stop.Token);
            }
        } finally {
            _listener.Stop();
            _listener = null;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _listener?.Stop();
        Exception? failure = null;
        try {
            await _forwards.DisposeAsync().ConfigureAwait(false);
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            await _devices.DisposeAsync().ConfigureAwait(false);
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

    public ValueTask ReleaseUsbDevicesAsync(
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _devices.ReleaseUsbDevicesAsync(
            cancellationToken);
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken) {
        client.NoDelay = true;
        var handler = new AdbServerConnectionHandler(
            client,
            _devices,
            _forwards,
            _options,
            _loggerFactory.CreateLogger<AdbServerConnectionHandler>(),
            Stop);
        await handler.RunAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private void Stop() {
        if (!_shutdown.IsCancellationRequested) {
            _shutdown.Cancel();
        }
    }

    private static TcpListener CreateListener(
        AdbServerDaemonOptions options) {
        if (options.Host.Equals(
            "localhost",
            StringComparison.OrdinalIgnoreCase)) {
            return new TcpListener(IPAddress.Loopback, options.Port);
        }

        if (!IPAddress.TryParse(options.Host, out var address)) {
            throw new ArgumentException(
                $"ADB server host '{options.Host}' is not an IP address.",
                nameof(options));
        }

        return new TcpListener(address, options.Port);
    }

    private static void ValidateOptions(
        AdbServerDaemonOptions options) {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Host);
        if (options.Port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The ADB server port must be between 1 and 65535.");
        }

        if (options.TrackDevicesInterval <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The device tracking interval must be positive.");
        }
    }
}
