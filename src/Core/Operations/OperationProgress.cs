namespace Ampheck.RockchipToolkit.Core.Operations;

public sealed record OperationProgress {
    public required string Kind { get; init; }
    public required string Message { get; init; }
    public TaskProgress? Progress { get; init; }
}
