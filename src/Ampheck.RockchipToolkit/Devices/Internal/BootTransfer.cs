namespace Ampheck.RockchipToolkit.Devices.Internal;

internal sealed record BootTransfer(
    string Description,
    ushort RequestCode,
    uint Offset,
    uint Size,
    uint Delay
);
