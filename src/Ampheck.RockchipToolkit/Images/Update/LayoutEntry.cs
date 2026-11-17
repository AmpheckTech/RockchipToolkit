namespace Ampheck.RockchipToolkit.Images.Update;

internal sealed record LayoutEntry(
    string SourcePath,
    byte[]? Prebuilt,
    long Size,
    long Offset,
    long PaddedSize
);
