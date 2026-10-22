namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitHello(
    string ServerGuid,
    string ConnectionGuid,
    string HostName,
    string ServerVersion,
    byte ProtocolMajor,
    byte ProtocolMinor,
    IReadOnlyList<string> Methods,
    IReadOnlyList<string> Capabilities);
