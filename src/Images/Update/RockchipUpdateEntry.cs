namespace Ampheck.RockchipToolkit.Images.Update;

/// <summary>One package entry inside a Rockchip RKAF update image.</summary>
public sealed record RockchipUpdateEntry(
    string Name,
    string FileName,
    long Offset,
    uint FlashOffset,
    uint FlashSize,
    uint PaddedSize,
    long Size
);
