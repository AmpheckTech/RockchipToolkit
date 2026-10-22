using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ampheck.RockchipToolkit.Rpc;

public static class ToolkitBroadcastDiscoveryClient {
    public static async Task<IReadOnlyList<ToolkitServerDescriptor>>
        DiscoverAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default) {
        using var client = new UdpClient(
            AddressFamily.InterNetwork);
        client.Client.EnableBroadcast = true;
        client.Client.Bind(new IPEndPoint(
            IPAddress.Any,
            0));
        var request = ToolkitBroadcastDiscovery.EncodeRequest();
        foreach (var endpoint in GetBroadcastEndpoints()) {
            try {
                await client.SendAsync(
                        request,
                        endpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch (SocketException) {
            }
        }

        using var deadline =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        deadline.CancelAfter(timeout);
        var servers = new Dictionary<
            string,
            ToolkitServerDescriptor>(
            StringComparer.OrdinalIgnoreCase);
        while (!deadline.IsCancellationRequested) {
            UdpReceiveResult response;
            try {
                response = await client.ReceiveAsync(deadline.Token)
                    .ConfigureAwait(false);
            } catch (OperationCanceledException) when (
                deadline.IsCancellationRequested) {
                break;
            } catch (SocketException) {
                break;
            }

            var value = ToolkitBroadcastDiscovery.DecodeResponse(
                response.Buffer);
            if (value is null) {
                continue;
            }

            servers.TryAdd(
                value.Value.InstanceId,
                new ToolkitServerDescriptor(
                    value.Value.InstanceId,
                    value.Value.HostName,
                    response.RemoteEndPoint.Address.ToString(),
                    value.Value.Port,
                    value.Value.TokenRequired));
        }

        return servers.Values.ToArray();
    }

    private static IReadOnlyList<IPEndPoint>
        GetBroadcastEndpoints() {
        var result = new HashSet<IPEndPoint>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces()) {
            try {
                if (network.OperationalStatus != OperationalStatus.Up) {
                    continue;
                }

                foreach (var unicast in network.GetIPProperties()
                    .UnicastAddresses) {
                    if (unicast.Address.AddressFamily !=
                            AddressFamily.InterNetwork ||
                        unicast.IPv4Mask is null) {
                        continue;
                    }

                    var address = unicast.Address.GetAddressBytes();
                    var mask = unicast.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (var index = 0; index < 4; index++) {
                        broadcast[index] = (byte)(
                            address[index] |
                            ~mask[index]);
                    }

                    result.Add(new IPEndPoint(
                        new IPAddress(broadcast),
                        ToolkitBroadcastDiscovery.Port));
                }
            } catch (Exception error) when (
                error is NetworkInformationException or
                NotSupportedException) {
            }
        }

        result.Add(new IPEndPoint(
            IPAddress.Loopback,
            ToolkitBroadcastDiscovery.Port));
        return [.. result];
    }
}
