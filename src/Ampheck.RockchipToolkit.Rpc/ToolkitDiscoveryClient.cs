using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

public static class ToolkitDiscoveryClient {
    public static async Task<IReadOnlyList<ToolkitServerDescriptor>>
        DiscoverAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default) {
        if (timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var results = await Task.WhenAll(
                TryDiscoverAsync(() =>
                    DiscoverMdnsAsync(timeout, cancellationToken)),
                TryDiscoverAsync(() =>
                    ToolkitBroadcastDiscoveryClient.DiscoverAsync(
                        timeout,
                        cancellationToken)))
            .ConfigureAwait(false);
        return results
            .SelectMany(value => value)
            .GroupBy(
                value => value.InstanceId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(server => server.HostName)
            .ThenBy(server => server.InstanceId)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ToolkitServerDescriptor>>
        TryDiscoverAsync(
            Func<Task<IReadOnlyList<ToolkitServerDescriptor>>> discover) {
        try {
            return await discover().ConfigureAwait(false);
        } catch (Exception error) when (
            error is SocketException or
            NotSupportedException or
            ArgumentException or
            InvalidOperationException) {
            return [];
        }
    }

    private static async Task<IReadOnlyList<ToolkitServerDescriptor>>
        DiscoverMdnsAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) {
        using var client = CreateClient();
        var query = MdnsPacketCodec.EncodeQuery(
            ToolkitDiscoveryProtocol.ServiceType);
        await SendQueryAsync(
                client,
                query,
                cancellationToken)
            .ConfigureAwait(false);

        using var deadline =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        deadline.CancelAfter(timeout);
        var pointers = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var services = new Dictionary<
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

            MdnsPacket packet;
            try {
                packet = MdnsPacketCodec.Parse(
                    response.Buffer);
            } catch (InvalidDataException) {
                continue;
            }

            var records = packet.Answers
                .Concat(packet.AdditionalRecords)
                .ToArray();
            foreach (var record in records) {
                if (record.Ttl == 0) {
                    continue;
                }

                if (record.Type == MdnsRecordType.Ptr &&
                    record.Name.Equals(
                        ToolkitDiscoveryProtocol.ServiceType,
                        StringComparison.OrdinalIgnoreCase) &&
                    record.TargetName is not null) {
                    pointers.Add(record.TargetName);
                }
            }

            foreach (var instance in pointers) {
                var srv = records.FirstOrDefault(record =>
                    record.Type == MdnsRecordType.Srv &&
                    record.Name.Equals(
                        instance,
                        StringComparison.OrdinalIgnoreCase) &&
                    record.TargetName is not null);
                var txt = records.FirstOrDefault(record =>
                    record.Type == MdnsRecordType.Txt &&
                    record.Name.Equals(
                        instance,
                        StringComparison.OrdinalIgnoreCase));
                if (srv?.TargetName is null || txt?.Text is null) {
                    continue;
                }

                var values = ParseTxt(txt.Text);
                if (!values.TryGetValue("guid", out var guid) ||
                    !values.TryGetValue("hostname", out var hostName) ||
                    !values.TryGetValue("auth", out var auth)) {
                    continue;
                }
                var address = records
                    .Where(record =>
                        record.Type == MdnsRecordType.A &&
                        record.Address is not null &&
                        record.Name.Equals(
                            srv.TargetName,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(record => record.Address!)
                    .OrderBy(value =>
                        value.Equals(response.RemoteEndPoint.Address)
                            ? 0
                            : value.Equals(IPAddress.Loopback)
                                ? 2
                                : 1)
                    .FirstOrDefault()
                    ?? response.RemoteEndPoint.Address;
                services.TryAdd(
                    guid,
                    new ToolkitServerDescriptor(
                        guid,
                        hostName,
                        address.ToString(),
                        srv.Port,
                        auth.Equals(
                            "token",
                            StringComparison.OrdinalIgnoreCase)));
            }
        }

        return services.Values
            .OrderBy(server => server.HostName)
            .ThenBy(server => server.InstanceId)
            .ToArray();
    }

    private static UdpClient CreateClient() {
        var client = new UdpClient(
            AddressFamily.InterNetwork);
        try {
            client.Client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                optionValue: true);
            client.Client.Bind(new IPEndPoint(
                IPAddress.Any,
                ToolkitDiscoveryProtocol.Port));
            client.MulticastLoopback = true;
            foreach (var value in GetMulticastInterfaces()) {
                try {
                    client.JoinMulticastGroup(
                        ToolkitDiscoveryProtocol.MulticastAddress,
                        value.Address);
                } catch (Exception error) when (
                    error is SocketException or
                    NotSupportedException or
                    ArgumentException) {
                }
            }
            return client;
        } catch {
            client.Dispose();
            throw;
        }
    }

    private static Dictionary<string, string> ParseTxt(
        IReadOnlyList<byte[]> values) {
        var result = new Dictionary<
            string,
            string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bytes in values) {
            var value = Encoding.UTF8.GetString(bytes);
            var separator = value.IndexOf('=');
            if (separator <= 0) {
                continue;
            }

            result[value[..separator]] =
                value[(separator + 1)..];
        }

        return result;
    }

    private static async Task SendQueryAsync(
        UdpClient client,
        byte[] query,
        CancellationToken cancellationToken) {
        var endpoint = new IPEndPoint(
            ToolkitDiscoveryProtocol.MulticastAddress,
            ToolkitDiscoveryProtocol.Port);
        foreach (var value in GetMulticastInterfaces()) {
            try {
                client.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                IPAddress.HostToNetworkOrder(value.Index));
                await client.SendAsync(
                        query,
                        endpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch (Exception error) when (
                error is SocketException or
                NotSupportedException or
                ArgumentException) {
            }
        }
    }

    private static IReadOnlyList<(int Index, IPAddress Address)>
        GetMulticastInterfaces() {
        var result = new List<(int Index, IPAddress Address)>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces()) {
            try {
                if (network.OperationalStatus != OperationalStatus.Up ||
                    !network.SupportsMulticast) {
                    continue;
                }

                var properties = network.GetIPProperties();
                var index = properties.GetIPv4Properties()?.Index;
                if (index is null) {
                    continue;
                }

                var address = properties.UnicastAddresses
                    .Select(value => value.Address)
                    .FirstOrDefault(value =>
                        value.AddressFamily ==
                            AddressFamily.InterNetwork);
                if (address is not null) {
                    result.Add((index.Value, address));
                }
            } catch (Exception error) when (
                error is NetworkInformationException or
                SocketException or NotSupportedException) {
            }
        }

        return result;
    }
}
