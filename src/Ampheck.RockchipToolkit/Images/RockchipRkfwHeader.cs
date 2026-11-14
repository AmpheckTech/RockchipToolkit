using System.Buffers.Binary;

namespace Ampheck.RockchipToolkit.Images;

internal static class RockchipRkfwHeader {
    private const uint FirmwareCode = 0x02000000;

    public static byte[] Create(
        string chip,
        uint firmwareVersion,
        long bootSize,
        long updateSize) {
        var header = new byte[RockchipImageLayout.RkfwHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, RockchipImageLayout.RkfwMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(
            header.AsSpan(RockchipImageLayout.RkfwHeaderSizeOffset),
            RockchipImageLayout.RkfwHeaderSize);

        // The RKFW chip field at 0x15 stores the chip number in reverse
        // byte order, for example RK3399 -> "9933".
        BinaryPrimitives.WriteUInt16LittleEndian(
            header.AsSpan(RockchipImageLayout.RkfwVersionOffset),
            (ushort)firmwareVersion);
        header[8] = (byte)(firmwareVersion >> 16);
        header[9] = (byte)(firmwareVersion >> 24);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(10),
            FirmwareCode);

        var now = DateTime.Now;
        BinaryPrimitives.WriteUInt16LittleEndian(
            header.AsSpan(14),
            (ushort)now.Year);
        header[16] = (byte)now.Month;
        header[17] = (byte)now.Day;
        header[18] = (byte)now.Hour;
        header[19] = (byte)now.Minute;
        header[20] = (byte)now.Second;
        WriteChip(header.AsSpan(21, 4), chip);
        // 0x2d is set in firmware headers produced by rkImageMaker.
        header[45] = 1;

        var bootOffset = (long)RockchipImageLayout.RkfwHeaderSize;
        var updateOffset = checked(bootOffset + bootSize);
        if (updateOffset > uint.MaxValue) {
            throw new InvalidDataException(
                "The BOOT image is too large for the RKFW header layout.");
        }

        // Offsets 0x19/0x1d hold BOOT offset/size; 0x21/0x25 hold the
        // embedded RKAF offset and the low 32 bits of its size. "HI" at
        // 0x37 and a high word at 0x39 extend the RKAF size beyond 4 GiB.
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkfwBootOffsetOffset),
            (uint)bootOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkfwBootSizeOffset),
            (uint)bootSize);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkfwUpdateOffsetOffset),
            (uint)updateOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkfwUpdateSizeOffset),
            (uint)updateSize);
        if (updateSize > uint.MaxValue) {
            header[RockchipImageLayout.RkfwSignedFlagOffset] = (byte)'H';
            header[RockchipImageLayout.RkfwSignedFlagOffset + 1] = (byte)'I';
            BinaryPrimitives.WriteUInt32LittleEndian(
                header.AsSpan(RockchipImageLayout.RkfwUpdateSizeHighOffset),
                (uint)(updateSize >> 32));
        }

        return header;
    }

    private static void WriteChip(Span<byte> field, string chip) {
        var value = chip.Trim().ToUpperInvariant();
        // Legacy rkImageMaker headers use numeric SoC codes instead of the
        // reversed name stored by modern four-digit RKxxxx headers.
        switch (value) {
            case "RK28":
                BinaryPrimitives.WriteUInt32LittleEndian(field, 0x20);
                return;
            case "RK30":
                BinaryPrimitives.WriteUInt32LittleEndian(field, 0x60);
                return;
            case "RK31":
                BinaryPrimitives.WriteUInt32LittleEndian(field, 0x70);
                return;
            case "RK32":
                BinaryPrimitives.WriteUInt32LittleEndian(field, 0x80);
                return;
            case "RKPANDA":
                BinaryPrimitives.WriteUInt32LittleEndian(field, 0x22);
                return;
        }

        if (value.Length != 6 ||
            !value.StartsWith("RK", StringComparison.OrdinalIgnoreCase) ||
            !value.AsSpan(2).ContainsOnlyDigits()) {
            throw new InvalidDataException(
                $"Unsupported RKFW chip '{chip}'.");
        }

        for (var index = 0; index < field.Length; index++) {
            field[index] = (byte)value[5 - index];
        }
    }

    private static bool ContainsOnlyDigits(this ReadOnlySpan<char> value) {
        foreach (var character in value) {
            if (!char.IsAsciiDigit(character)) {
                return false;
            }
        }

        return value.Length > 0;
    }
}
