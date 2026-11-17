namespace Ampheck.RockchipToolkit;

public readonly record struct OperationProgress(
    ToolkitOperationStage Stage,
    string? Item,
    long Completed,
    long Total
);
