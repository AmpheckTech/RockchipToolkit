namespace Ampheck.RockchipToolkit.Images;

/// <summary>One package entry inside a Rockchip RKAF update image.</summary>
public sealed record RockchipUpdateEntry(
    string Name,
    string FileName,
    long Offset,
    uint FlashOffset,
    uint UseSpace,
    uint Size);
