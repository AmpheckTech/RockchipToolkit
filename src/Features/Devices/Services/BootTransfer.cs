namespace Ampheck.RockchipToolkit.Features.Devices.Services;

internal sealed record BootTransfer(
    string Description,
    ushort RequestCode,
    uint Offset,
    uint Size,
    uint Delay
);
