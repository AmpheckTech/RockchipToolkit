using System.Net;
using System.Net.Sockets;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Server;

internal sealed class ToolkitBroadcastResponder(
    ToolkitHostOptions options) : IAsyncDisposable {
    private readonly CancellationTokenSource _stop = new();
    private UdpClient? _client;
    private Task? _loop;

    public void Start() {
        var client = new UdpClient(
            AddressFamily.InterNetwork);
        try {
            client.Client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                optionValue: true);
            client.Client.EnableBroadcast = true;
            client.Client.Bind(new IPEndPoint(
                IPAddress.Any,
                ToolkitBroadcastDiscovery.Port));
            _client = client;
            _loop = Task.Run(RunAsync);
        } catch {
            client.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await _stop.CancelAsync().ConfigureAwait(false);
        _client?.Dispose();
        if (_loop is not null) {
            try {
                await _loop.ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }

        _stop.Dispose();
    }

    private async Task RunAsync() {
        var response = ToolkitBroadcastDiscovery.EncodeResponse(
            options.InstanceId,
            options.HostName,
            options.Endpoint.Port,
            options.Token is not null);
        while (!_stop.IsCancellationRequested) {
            UdpReceiveResult request;
            try {
                request = await _client!.ReceiveAsync(_stop.Token)
                    .ConfigureAwait(false);
            } catch (OperationCanceledException) when (
                _stop.IsCancellationRequested) {
                return;
            } catch (SocketException) when (
                _stop.IsCancellationRequested) {
                return;
            }

            if (!ToolkitBroadcastDiscovery.IsRequest(
                    request.Buffer)) {
                continue;
            }

            try {
                await _client.SendAsync(
                        response,
                        request.RemoteEndPoint,
                        _stop.Token)
                    .ConfigureAwait(false);
            } catch (SocketException) {
            }
        }
    }
}
