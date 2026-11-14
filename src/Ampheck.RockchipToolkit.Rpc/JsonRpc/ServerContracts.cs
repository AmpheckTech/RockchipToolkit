namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record ServerInfoResult {
    public required string InstanceId { get; init; }
    public required string HostName { get; init; }
    public required string Version { get; init; }
    public required int ProtocolMajor { get; init; }
    public required int ProtocolMinor { get; init; }
    public required string[] Methods { get; init; }
}
