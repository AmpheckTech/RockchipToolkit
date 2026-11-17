using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Devices;

public sealed record DeviceInfo(
    RockchipChipInfo Chip,
    RockchipFlashInfo Flash
);
