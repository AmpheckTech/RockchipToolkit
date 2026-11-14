namespace Ampheck.RockchipToolkit.Images;

internal static class RockchipImageLayout {
    public const uint RkfwMagic = 0x57464B52;
    public const int RkfwHeaderSize = 102;
    public const int RkfwHeaderSizeOffset = 4;
    public const int RkfwVersionOffset = 6;
    public const int RkfwBootOffsetOffset = 25;
    public const int RkfwBootSizeOffset = 29;
    public const int RkfwUpdateOffsetOffset = 33;
    public const int RkfwUpdateSizeOffset = 37;
    public const int RkfwSignedFlagOffset = 55;
    public const int RkfwUpdateSizeHighOffset = 57;
    public const int RkafHeaderSize = 2048;
    public const int RkafLengthOffset = 4;
    public const int RkafMachineModelOffset = 8;
    public const int RkafMachineModelSize = 34;
    public const int RkafMachineIdOffset = 42;
    public const int RkafMachineIdSize = 30;
    public const int RkafManufacturerOffset = 72;
    public const int RkafManufacturerSize = 56;
    public const int RkafVersionOffset = 132;
    public const int RkafEntryCountOffset = 136;
    public const int RkafEntrySize = 112;
    public const int RkafMaximumEntries = 16;
    public const int RkafEntryOffset = 140;
    public const int RkafEntryNameSize = 32;
    public const int RkafEntryPathOffset = 32;
    public const int RkafEntryPathSize = 60;
    public const int RkafEntryFlashSizeOffset = 92;
    public const int RkafEntryStoredOffsetOffset = 96;
    public const int RkafEntryFlashOffsetOffset = 100;
    public const int RkafEntryPaddedSectorsOffset = 104;
    public const int RkafEntryStoredSizeOffset = 108;
    public const int RkafChecksumSize = 4;
    public const int SectorSize = 2048;
    public const long UInt32Range = 1L << 32;

    /// <summary>
    /// RKAF stores offsets and byte counts in 32-bit fields even when an image
    /// is larger than 4 GiB. The true value is the largest congruent value that
    /// still fits in the known allocation.
    /// </summary>
    public static long RecoverUInt32Value(uint stored, long available) {
        var value = (long)stored;
        if (available <= value) {
            return value;
        }

        return value + (((available - value) / UInt32Range) * UInt32Range);
    }

    public static long AlignUp(long value, int alignment) {
        if (value < 0) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var remainder = value % alignment;
        return remainder == 0 ? value : checked(value + alignment - remainder);
    }
}
