using Ampheck.Adb;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Server;

public sealed record ToolkitHostOptions {
    public ToolkitEndpoint Endpoint { get; init; } =
        new(ToolkitServerDefaults.Host, ToolkitServerDefaults.Port);
    public string? Token { get; init; }
    public string InstanceId { get; init; } =
        Guid.NewGuid().ToString("D");
    public string HostName { get; init; } =
        Environment.MachineName;
    public TimeSpan IdleTimeout { get; init; } =
        Timeout.InfiniteTimeSpan;
    public ToolkitTransferLimits TransferLimits { get; init; } =
        ToolkitTransferLimits.Default;
    public int SerialHistoryBytes { get; init; } =
        256 * 1024;
    public int AdbPort { get; init; } = AdbDefaults.ServerPort;
}
