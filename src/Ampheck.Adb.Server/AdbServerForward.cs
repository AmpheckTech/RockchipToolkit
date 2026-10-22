using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Ampheck.Adb.Server;

internal sealed class AdbServerForward : IAsyncDisposable {
    private readonly string _serialNumber;
    private readonly string _remoteSpec;
    private readonly TcpListener _listener;
    private readonly AdbServerDeviceRegistry _devices;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptLoop;
    private bool _disposed;

    public AdbServerForward(
        string serialNumber,
        string remoteSpec,
        int localPort,
        AdbServerDeviceRegistry devices,
        ILogger logger) {
        _serialNumber = serialNumber;
        _remoteSpec = remoteSpec;
        _listener = new TcpListener(IPAddress.Loopback, localPort);
        _devices = devices;
        _logger = logger;
    }

    public string SerialNumber => _serialNumber;
    public string RemoteSpec => _remoteSpec;

    public int LocalPort {
        get {
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            return endpoint.Port;
        }
    }

    public string LocalSpec => $"tcp:{LocalPort}";

    public void Start() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        _listener.Stop();
        if (_acceptLoop is not null) {
            try {
                await _acceptLoop.ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }

        _stop.Dispose();
    }

    private async Task AcceptLoopAsync() {
        while (!_stop.IsCancellationRequested) {
            TcpClient client;
            try {
                client = await _listener.AcceptTcpClientAsync(
                        _stop.Token)
                    .ConfigureAwait(false);
            } catch (OperationCanceledException) when (
                _stop.IsCancellationRequested) {
                return;
            } catch (SocketException) when (
                _stop.IsCancellationRequested) {
                return;
            }

            _ = HandleClientAsync(client, _stop.Token);
        }
    }

    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken cancellationToken) {
        client.NoDelay = true;
        try {
            var device = await _devices.RequireAsync(
                    _serialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            await using var lease = await device.OpenStreamAsync(
                    _remoteSpec,
                    cancellationToken)
                .ConfigureAwait(false);
            await using var stream = client.GetStream();
            await AdbServerStreamBridge.RunAsync(
                    stream,
                    lease.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is IOException or
            InvalidOperationException or
            SocketException or
            OperationCanceledException or
            UnauthorizedAccessException or
            System.ComponentModel.Win32Exception) {
            _logger.LogDebug(
                error,
                "ADB forward {LocalSpec} to {RemoteSpec} failed",
                LocalSpec,
                _remoteSpec);
        } finally {
            client.Dispose();
        }
    }
}
