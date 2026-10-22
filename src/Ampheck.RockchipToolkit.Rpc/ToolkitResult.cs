namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitResult(
    bool Success,
    int ExitCode,
    string? Error,
    byte[]? Output = null);
