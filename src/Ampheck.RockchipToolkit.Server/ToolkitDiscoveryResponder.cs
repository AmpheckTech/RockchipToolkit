using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

internal sealed class ToolkitDiscoveryResponder : IAsyncDisposable {
    private const uint ServiceTtl = 4500;
    private const uint HostTtl = 120;
    private readonly ToolkitHostOptions _options;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly string _instanceName;
    private readonly string _hostTarget;
    private readonly byte[] _announcement;
    private readonly byte[] _goodbye;
    private IReadOnlyList<MdnsInterface> _interfaces = [];
    private UdpClient? _client;
    private Task? _loop;

    public ToolkitDiscoveryResponder(ToolkitHostOptions options, ILogger logger) {
        _options = options;
        _logger = logger;
        var identity = options.InstanceId
            .Replace("-", string.Empty, StringComparison.Ordinal)[..8];
        var label = SanitizeLabel(options.HostName);
        _instanceName =
            $"{label}-{identity}." +
            ToolkitDiscoveryProtocol.ServiceType;
        _hostTarget =
            $"{label}-{identity}.local.";
        _announcement = CreateResponse(ttlMultiplier: 1);
        _goodbye = CreateResponse(ttlMultiplier: 0);
    }

    public void Start() {
        var client = new UdpClient(
            AddressFamily.InterNetwork);
        try {
            client.Client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, ToolkitDiscoveryProtocol.Port));
            client.MulticastLoopback = true;
            client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, optionValue: 255);
            var joined = new List<MdnsInterface>();
            foreach (var networkInterface in GetMdnsInterfaces(_options)) {
                try {
                    client.JoinMulticastGroup(
                        ToolkitDiscoveryProtocol.MulticastAddress,
                        networkInterface.Address);
                    joined.Add(networkInterface);
                } catch (Exception error) when (
                    error is SocketException or
                    NotSupportedException or
                    ArgumentException) {
                    _logger.LogDebug(
                        error,
                        "Skipping mDNS interface {Address}",
                        networkInterface.Address);
                }
            }

            if (joined.Count == 0) {
                throw new SocketException(
                    (int)SocketError.AddressNotAvailable);
            }

            _interfaces = joined;
            _client = client;
            _loop = Task.Run(RunAsync);
        } catch {
            client.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_client is not null) {
            try {
                await SendAsync(_goodbye, CancellationToken.None)
                    .ConfigureAwait(false);
            } catch (Exception error) when (error is SocketException or ObjectDisposedException) {
            }
        }

        _stop.Cancel();
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
        await SendAsync(_announcement, _stop.Token)
            .ConfigureAwait(false);
        while (!_stop.IsCancellationRequested) {
            UdpReceiveResult request;
            try {
                request = await _client!.ReceiveAsync(_stop.Token)
                    .ConfigureAwait(false);
            } catch (OperationCanceledException) when (_stop.IsCancellationRequested) {
                return;
            } catch (SocketException) when (_stop.IsCancellationRequested) {
                return;
            }

            try {
                var packet = MdnsPacketCodec.Parse(request.Buffer);
                if (!ShouldRespond(packet)) {
                    continue;
                }

                await SendAsync(_announcement, _stop.Token)
                    .ConfigureAwait(false);
            } catch (InvalidDataException) {
            } catch (Exception error) when (error is SocketException or OperationCanceledException) {
                _logger.LogDebug(error, "Unable to answer toolkit mDNS query");
            }
        }
    }

    private bool ShouldRespond(MdnsPacket packet) {
        foreach (var question in packet.Questions) {
            if (question.Name.Equals(ToolkitDiscoveryProtocol.ServiceType, StringComparison.OrdinalIgnoreCase) &&
                question.Type is MdnsRecordType.Ptr or MdnsRecordType.Any) {
                return true;
            }

            if (question.Name.Equals(_instanceName, StringComparison.OrdinalIgnoreCase) ||
                question.Name.Equals(_hostTarget, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private async Task SendAsync(byte[] payload, CancellationToken cancellationToken) {
        var endpoint = new IPEndPoint(
            ToolkitDiscoveryProtocol.MulticastAddress,
            ToolkitDiscoveryProtocol.Port);
        foreach (var networkInterface in _interfaces) {
            try {
                _client!.Client.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.MulticastInterface,
                    IPAddress.HostToNetworkOrder(
                        networkInterface.Index));
                await _client.SendAsync(
                        payload,
                        endpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch (Exception error) when (
                error is SocketException or
                NotSupportedException or
                ArgumentException) {
                _logger.LogDebug(
                    error,
                    "Unable to send mDNS on interface {Address}",
                    networkInterface.Address);
            }
        }
    }

    private byte[] CreateResponse(uint ttlMultiplier) {
        var pointer = MdnsPacketCodec.CreatePtr(
            ToolkitDiscoveryProtocol.ServiceType,
            _instanceName,
            ServiceTtl * ttlMultiplier);
        var txt = new List<byte[]> {
            MdnsPacketCodec.ToTxtBytes(
                $"proto={ToolkitProtocolVersion.Major}." +
                $"{ToolkitProtocolVersion.Minor}"),
            MdnsPacketCodec.ToTxtBytes(
                $"caps={string.Join(",", ToolkitCapabilities.All)}"),
            MdnsPacketCodec.ToTxtBytes(
                $"guid={_options.InstanceId}"),
            MdnsPacketCodec.ToTxtBytes(
                $"hostname={_options.HostName}"),
            MdnsPacketCodec.ToTxtBytes(
                $"auth={(_options.Token is null ? "none" : "token")}")
        };
        var additional = new List<MdnsResourceRecord> {
            MdnsPacketCodec.CreateSrv(
                _instanceName,
                _hostTarget,
                checked((ushort)_options.Endpoint.Port),
                HostTtl * ttlMultiplier),
            MdnsPacketCodec.CreateTxt(
                _instanceName,
                txt,
                HostTtl * ttlMultiplier)
        };
        foreach (var address in GetAdvertisedAddresses(_options)) {
            additional.Add(MdnsPacketCodec.CreateA(
                _hostTarget,
                address,
                HostTtl * ttlMultiplier));
        }

        return MdnsPacketCodec.EncodeResponse(pointer, additional);
    }

    private static IReadOnlyList<IPAddress> GetAdvertisedAddresses(ToolkitHostOptions options) {
        if (options.Endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) {
            return [IPAddress.Loopback];
        }

        if (IPAddress.TryParse(options.Endpoint.Host, out var configured) &&
            !configured.Equals(IPAddress.Any) && !configured.Equals(IPAddress.IPv6Any)) {
            return [configured];
        }

        var addresses = GetAllInterfaces()
            .Select(value => value.Address)
            .Distinct()
            .ToList();

        if (addresses.Count == 0) {
            addresses.Add(IPAddress.Loopback);
        }

        return addresses.Distinct().ToArray();
    }

    private static IReadOnlyList<MdnsInterface> GetMdnsInterfaces(
        ToolkitHostOptions options) {
        if (IPAddress.TryParse(
                options.Endpoint.Host,
                out var configured) &&
            !configured.Equals(IPAddress.Any)) {
            var configuredInterface = GetAllInterfaces()
                .FirstOrDefault(value =>
                    value.Address.Equals(configured));
            return configuredInterface is null
                ? []
                : [configuredInterface];
        }

        return [
            .. GetAllInterfaces()
                .DistinctBy(value => value.Index)
        ];
    }

    private static IReadOnlyList<MdnsInterface> GetAllInterfaces() {
        var result = new List<MdnsInterface>();
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

                foreach (var unicast in properties.UnicastAddresses) {
                    if (unicast.Address.AddressFamily ==
                        AddressFamily.InterNetwork) {
                        result.Add(new MdnsInterface(
                            network,
                            index.Value,
                            unicast.Address));
                    }
                }
            } catch (Exception error) when (
                error is NetworkInformationException or
                SocketException or NotSupportedException) {
            }
        }

        return result;
    }

    private static string SanitizeLabel(string value) {
        var label = new string(value.Select(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray())
            .Trim('-');
        if (label.Length == 0) {
            label = "arkt";
        }

        return label.Length <= 40 ? label : label[..40];
    }
}
