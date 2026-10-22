namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitEvent(
    string Source,
    string Message,
    long? Current = null,
    long? Total = null);
