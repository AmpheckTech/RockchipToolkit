namespace Ampheck.RockchipToolkit.Protocol;

/// <summary>Decoded storage information returned by the RockUSB flash-info command.</summary>
public sealed record RockchipFlashInfo(
    string Manufacturer,
    byte ManufacturerCode,
    uint SectorCount,
    ushort SectorsPerBlock,
    byte PageSizeCode,
    byte EccBits,
    byte AccessTime,
    byte ChipSelectMask) {
    public ulong CapacityBytes =>
        (ulong)SectorCount * RockUsbProtocol.SectorSize;

    public uint BlockSizeBytes =>
        checked((uint)SectorsPerBlock * RockUsbProtocol.SectorSize);

    public uint PageSizeBytes =>
        checked((uint)PageSizeCode * RockUsbProtocol.SectorSize);

    public uint BlockCount =>
        SectorsPerBlock == 0
            ? 0
            : SectorCount / SectorsPerBlock;
}
