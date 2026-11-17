namespace Ampheck.RockchipToolkit.Protocol.RockUsb;

internal static class RockUsbCommandPolicy {
    public static byte GetCommandBlockLength(RockUsbOperation operation) {
        return operation switch {
            RockUsbOperation.ReadLba or
            RockUsbOperation.WriteLba or
            RockUsbOperation.EraseLba => 0x0A, // 10-byte vendor command block.
            _ => 0x06 // Opcode and subcode only.
        };
    }

    public static bool CanRetry(RockUsbOperation operation) {
        return operation is
            RockUsbOperation.ReadFlashId or
            RockUsbOperation.ReadLba or
            RockUsbOperation.ReadFlashInfo or
            RockUsbOperation.ReadChipInfo or
            RockUsbOperation.ReadCapability;
    }

    public static void ValidateSectorCount(ushort sectorCount) {
        if (sectorCount == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(sectorCount),
                "The sector count must be at least one.");
        }
    }

    public static void ValidateSectorRange(
        uint startSector,
        ulong sectorCount
    ) {
        if ((ulong)startSector + sectorCount >
            (ulong)uint.MaxValue + 1) {
            throw new ArgumentOutOfRangeException(
                nameof(startSector),
                "The sector range exceeds the 32-bit RockUSB address space.");
        }
    }
}
