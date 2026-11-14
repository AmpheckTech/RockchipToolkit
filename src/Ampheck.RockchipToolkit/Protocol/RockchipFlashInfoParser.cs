using System.Buffers.Binary;

namespace Ampheck.RockchipToolkit.Protocol;

public static class RockchipFlashInfoParser {
    private const int FlashInfoLength = 11;

    public static RockchipFlashInfo Parse(ReadOnlySpan<byte> data) {
        if (data.Length < FlashInfoLength) {
            throw new InvalidDataException(
                "The flash-info response is shorter than eleven bytes.");
        }

        // Response offsets (little-endian multi-byte fields): sector count u32
        // at 0, sectors per block u16 at 4, page size code u8 at 6, ECC bits
        // u8 at 7, access time u8 at 8, manufacturer u8 at 9, chip select u8 at 10.
        var sectorCount = BinaryPrimitives.ReadUInt32LittleEndian(data);
        var sectorsPerBlock =
            BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        var pageSizeCode = data[6];
        if (sectorsPerBlock == 0 || pageSizeCode == 0) {
            throw new InvalidDataException(
                "The flash-info response contains an invalid block or page size.");
        }

        var manufacturerCode = data[9];
        return new RockchipFlashInfo(
            GetManufacturerName(manufacturerCode),
            manufacturerCode,
            sectorCount,
            sectorsPerBlock,
            pageSizeCode,
            data[7],
            data[8],
            data[10]);
    }

    private static string GetManufacturerName(byte code) {
        return code switch {
            0 => "Samsung",
            1 => "Toshiba",
            2 => "Hynix",
            3 => "Infineon",
            4 => "Micron",
            5 => "Renesas",
            6 => "ST",
            7 => "Intel",
            _ => "Unknown"
        };
    }
}
