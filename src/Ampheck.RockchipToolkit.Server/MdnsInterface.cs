using System.Net;
using System.Net.NetworkInformation;

namespace Ampheck.RockchipToolkit.Server;

internal sealed record MdnsInterface(
    NetworkInterface Network,
    int Index,
    IPAddress Address);
