namespace Ampheck.RockchipToolkit.Images;

public sealed record RockchipPartition(
    string Name,
    uint Offset,
    uint? Size);
