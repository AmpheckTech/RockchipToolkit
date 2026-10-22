namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitServerDescriptor(
    string InstanceId,
    string HostName,
    string Host,
    int Port,
    bool TokenRequired) {
    public ToolkitEndpoint Endpoint => new(Host, Port);
}
