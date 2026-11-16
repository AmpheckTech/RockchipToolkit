namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record LoaderImageEntry(
    string Name,
    ushort RequestCode,
    uint Offset,
    uint Size,
    uint Delay
);
