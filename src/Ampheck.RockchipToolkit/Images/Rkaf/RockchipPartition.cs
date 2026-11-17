namespace Ampheck.RockchipToolkit.Images.Rkaf;

public sealed record RockchipPartition(
    string Name,
    uint Offset,
    uint? Size
);
