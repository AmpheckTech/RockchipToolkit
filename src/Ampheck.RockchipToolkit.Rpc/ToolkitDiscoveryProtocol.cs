using System.Net;

namespace Ampheck.RockchipToolkit.Rpc;

internal static class ToolkitDiscoveryProtocol {
    public const int Port = 5353;
    public const string ServiceType = "_arkt._tcp.local.";
    public static readonly IPAddress MulticastAddress =
        IPAddress.Parse("224.0.0.251");
}
