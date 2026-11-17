namespace Ampheck.RockchipToolkit.Devices;

internal sealed record LoaderImageEntry(
    string Name,
    ushort RequestCode,
    uint Offset,
    uint Size,
    uint Delay
);
