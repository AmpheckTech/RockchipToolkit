using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Images;

internal static class RockchipImageReader {
    private const int HeaderLength = 102;
    private const int BootEntryLength = 57;
    private const uint ImageMagic = 0x57464B52;
    private const uint BootMagic = 0x544F4F42;
    private const uint LegacyBootMagic = 0x2052444C;

    private static readonly (int CountOffset, int OffsetOffset, int SizeOffset, BootEntryType Type)[] BootTables = [
        (25, 26, 30, BootEntryType.Code471),
        (31, 32, 36, BootEntryType.Code472),
        (37, 38, 42, BootEntryType.Loader)
    ];

    public static (RockchipImageHeader Header, IReadOnlyList<RockchipBootEntry> Entries) Read(FileStream stream) {
        if (stream.Length < HeaderLength) {
            throw new InvalidDataException("The file is too small to contain a Rockchip image header.");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        StreamRange.ReadExactly(stream, header);

        var imageHeader = U32(header, 0) switch {
            ImageMagic => ReadFirmwareHeader(stream, header),
            BootMagic or LegacyBootMagic => ReadBootHeader(stream, header),
            _ => throw new InvalidDataException("Invalid RKIMAGE or BOOT signature.")
        };

        return (imageHeader, ReadBootEntries(stream, imageHeader));
    }

    private static RockchipImageHeader ReadFirmwareHeader(
        FileStream stream,
        ReadOnlySpan<byte> header) {
        var headerSize = U16(header, 4);
        if (headerSize < HeaderLength || headerSize > stream.Length) {
            throw new InvalidDataException("The RKIMAGE header size is invalid.");
        }

        var bootOffset = U32(header, 25);
        var bootSize = U32(header, 29);
        var firmwareOffset = U32(header, 33);
        var signed = header[55] == 'H' && header[56] == 'I';
        var firmwareSize = signed
            ? checked(((long)U32(header, 57) << 32) + U32(header, 37))
            : U32(header, 37);

        StreamRange.Validate(stream.Length, bootOffset, bootSize, "boot");
        StreamRange.Validate(stream.Length, firmwareOffset, firmwareSize, "firmware");

        return new RockchipImageHeader(
            U32(header, 6),
            U32(header, 10),
            headerSize,
            bootOffset,
            bootSize,
            firmwareOffset,
            firmwareSize,
            U32(header, 21),
            signed,
            RockchipImageKind.Firmware);
    }

    private static RockchipImageHeader ReadBootHeader(
        FileStream stream,
        ReadOnlySpan<byte> header) {
        if (stream.Length > uint.MaxValue) {
            throw new InvalidDataException("Standalone BOOT images larger than 4 GiB are not supported.");
        }

        var headerSize = U16(header, 4);
        if (headerSize < HeaderLength || headerSize > stream.Length) {
            throw new InvalidDataException("The BOOT header size is invalid.");
        }

        return new RockchipImageHeader(
            U32(header, 6),
            U32(header, 10),
            headerSize,
            0,
            checked((uint)stream.Length),
            0,
            0,
            U32(header, 21),
            header[43] == 'S',
            RockchipImageKind.Boot);
    }

    private static IReadOnlyList<RockchipBootEntry> ReadBootEntries(
        FileStream stream,
        RockchipImageHeader header) {
        if (header.BootSize < HeaderLength ||
            header.BootOffset > stream.Length ||
            header.BootSize > stream.Length - header.BootOffset) {
            return Array.Empty<RockchipBootEntry>();
        }

        if (header.BootSize > int.MaxValue) {
            throw new InvalidDataException("The BOOT image is too large to parse.");
        }

        var data = new byte[checked((int)header.BootSize)];
        StreamRange.ReadExactly(stream, data, header.BootOffset);
        if (U32(data, 0) is not (BootMagic or LegacyBootMagic)) {
            return Array.Empty<RockchipBootEntry>();
        }

        var entryCount = data[25] + data[31] + data[37];
        var entries = new List<RockchipBootEntry>(entryCount);
        foreach (var table in BootTables) {
            ReadTable(data, table, entries);
        }

        return entries.AsReadOnly();
    }

    private static void ReadTable(
        byte[] data,
        (int CountOffset, int OffsetOffset, int SizeOffset, BootEntryType Type) table,
        List<RockchipBootEntry> entries) {
        var entryCount = data[table.CountOffset];
        if (entryCount == 0) {
            return;
        }

        var entrySize = data[table.SizeOffset];
        if (entrySize < BootEntryLength) {
            throw new InvalidDataException(
                $"The {table.Type} entry size is smaller than the BOOT entry structure.");
        }

        var tableOffset = U32(data, table.OffsetOffset);
        for (var index = 0; index < entryCount; index++) {
            var position = checked(tableOffset + (long)index * entrySize);
            if (position + BootEntryLength > data.Length) {
                throw new InvalidDataException("The BOOT entry table is truncated.");
            }

            var start = checked((int)position);
            var storedType = (BootEntryType)U32(data, start + 1);
            if (storedType != table.Type) {
                throw new InvalidDataException(
                    $"The BOOT entry type {storedType} does not match its {table.Type} table.");
            }

            var name = DecodeEntryName(data.AsSpan(start + 5, 40));
            var dataOffset = U32(data, start + 45);
            var dataSize = U32(data, start + 49);
            var delay = U32(data, start + 53);
            StreamRange.Validate(data.Length, dataOffset, dataSize, $"BOOT entry '{name}'");
            entries.Add(new RockchipBootEntry(table.Type, name, dataOffset, dataSize, delay));
        }
    }

    private static string DecodeEntryName(ReadOnlySpan<byte> data) {
        var name = Encoding.Unicode.GetString(data);
        var terminator = name.IndexOf('\0');
        return terminator >= 0 ? name[..terminator] : name;
    }

    private static ushort U16(ReadOnlySpan<byte> data, int offset) {
        return BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) {
        return BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }
}
