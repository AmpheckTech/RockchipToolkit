namespace Ampheck.RockchipToolkit.Images.Rkaf;

internal sealed record RockchipRkafHeaderEntry(
    string Name,
    string FileName,
    long Offset,
    long Size,
    long PaddedSize
);
