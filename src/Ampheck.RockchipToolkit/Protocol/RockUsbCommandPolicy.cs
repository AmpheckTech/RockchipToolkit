namespace Ampheck.RockchipToolkit.Protocol;

internal static class RockUsbCommandPolicy {
    public static byte GetCommandBlockLength(RockUsbOperation operation) {
        return operation switch {
            RockUsbOperation.TestBadBlock or
            RockUsbOperation.ReadSector or
            RockUsbOperation.WriteSector or
            RockUsbOperation.EraseNormal or
            RockUsbOperation.EraseForce or
            RockUsbOperation.ReadLba or
            RockUsbOperation.WriteLba or
            RockUsbOperation.ReadSdram or
            RockUsbOperation.WriteSdram or
            RockUsbOperation.ExecuteSdram or
            RockUsbOperation.WriteEfuse or
            RockUsbOperation.ReadSpiFlash or
            RockUsbOperation.WriteSpiFlash or
            RockUsbOperation.WriteNewEfuse or
            RockUsbOperation.ReadNewEfuse or
            RockUsbOperation.EraseLba => 0x0A,
            _ => 0x06
        };
    }

    public static bool CanRetry(RockUsbOperation operation) {
        return operation is
            RockUsbOperation.TestUnitReady or
            RockUsbOperation.ReadFlashId or
            RockUsbOperation.TestBadBlock or
            RockUsbOperation.ReadSector or
            RockUsbOperation.ReadLba or
            RockUsbOperation.ReadSdram or
            RockUsbOperation.ReadFlashInfo or
            RockUsbOperation.ReadChipInfo or
            RockUsbOperation.ReadEfuse or
            RockUsbOperation.ReadSpiFlash or
            RockUsbOperation.ReadNewEfuse or
            RockUsbOperation.ReadStorage or
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
        ulong sectorCount) {
        if ((ulong)startSector + sectorCount >
            (ulong)uint.MaxValue + 1) {
            throw new ArgumentOutOfRangeException(
                nameof(startSector),
                "The sector range exceeds the 32-bit RockUSB address space.");
        }
    }
}
