using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Images;

internal static class RockchipImageReader {
    private const int HeaderLength = RockchipImageLayout.RkfwHeaderSize;
    private const int BootEntryLength = 57;
    private const uint BootMagic = 0x544F4F42;
    private const uint LegacyBootMagic = 0x2052444C;

    private static readonly (int CountOffset, int OffsetOffset, int SizeOffset, BootEntryType Type)[] BootTables = [
        (25, 26, 30, BootEntryType.Code471),
        (31, 32, 36, BootEntryType.Code472),
        (37, 38, 42, BootEntryType.Loader)
    ];

    public static (
        RockchipImageHeader Header,
        RockchipImageKind BootKind,
        ushort BootHeaderSize,
        IReadOnlyList<RockchipBootEntry> Entries) Read(Stream stream) {
        if (stream.Length < HeaderLength) {
            throw new InvalidDataException("The file is too small to contain a Rockchip image header.");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        StreamRange.ReadExactly(stream, header, 0);

        var magic = U32(header, 0);
        if (RockchipNewIdbReader.IsMagic(magic)) {
            var result = RockchipNewIdbReader.Read(stream, magic);
            return (
                result.Header,
                result.Header.Kind,
                result.Header.HeaderSize,
                result.Entries);
        }

        var imageHeader = magic switch {
            RockchipImageLayout.RkfwMagic => ReadFirmwareHeader(stream, header),
            BootMagic or LegacyBootMagic => ReadBootHeader(stream, header),
            _ => throw new InvalidDataException(
                "Invalid RKIMAGE, BOOT, RKNS, or RKSS signature.")
        };

        var boot = ReadBootEntries(stream, imageHeader);
        return (
            imageHeader,
            boot.Kind,
            boot.HeaderSize,
            boot.Entries);
    }

    private static RockchipImageHeader ReadFirmwareHeader(
        Stream stream,
        ReadOnlySpan<byte> header) {
        var headerSize = U16(header, RockchipImageLayout.RkfwHeaderSizeOffset);
        if (headerSize < HeaderLength || headerSize > stream.Length) {
            throw new InvalidDataException("The RKIMAGE header size is invalid.");
        }

        var bootOffset = U32(header, RockchipImageLayout.RkfwBootOffsetOffset);
        var bootSize = U32(header, RockchipImageLayout.RkfwBootSizeOffset);
        var firmwareOffset = U32(header, RockchipImageLayout.RkfwUpdateOffsetOffset);
        // RKFW header fields are little-endian. Offsets 0x19/0x1d hold the
        // BOOT offset/size and 0x21/0x25 hold the embedded RKAF offset and
        // the low 32 bits of its size. "HI" at 0x37 adds the high size word.
        var signed =
            header[RockchipImageLayout.RkfwSignedFlagOffset] == 'H' &&
            header[RockchipImageLayout.RkfwSignedFlagOffset + 1] == 'I';
        var firmwareEnd = HasTrailingMd5(stream)
            ? stream.Length - 32
            : stream.Length;
        var available = firmwareEnd - firmwareOffset;
        var storedFirmwareSize = U32(header, RockchipImageLayout.RkfwUpdateSizeOffset);
        var firmwareSize = signed
            ? checked(
                ((long)U32(header, RockchipImageLayout.RkfwUpdateSizeHighOffset) << 32) +
                storedFirmwareSize)
            : RockchipImageLayout.RecoverUInt32Value(
                storedFirmwareSize,
                available);

        StreamRange.Validate(stream.Length, bootOffset, bootSize, "boot");
        StreamRange.Validate(stream.Length, firmwareOffset, firmwareSize, "firmware");

        return new RockchipImageHeader(
            U32(header, RockchipImageLayout.RkfwVersionOffset),
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

    public static bool HasTrailingMd5(Stream stream) {
        if (stream.Length < 32) {
            return false;
        }

        Span<byte> tail = stackalloc byte[32];
        StreamRange.ReadExactly(stream, tail, stream.Length - 32);
        foreach (var value in tail) {
            if (!Uri.IsHexDigit((char)value)) {
                return false;
            }
        }

        return true;
    }

    private static RockchipImageHeader ReadBootHeader(
        Stream stream,
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

    private static (
        RockchipImageKind Kind,
        ushort HeaderSize,
        IReadOnlyList<RockchipBootEntry> Entries) ReadBootEntries(
        Stream stream,
        RockchipImageHeader header) {
        if (header.BootSize < HeaderLength ||
            header.BootOffset > stream.Length ||
            header.BootSize > stream.Length - header.BootOffset) {
            return (
                RockchipImageKind.Boot,
                HeaderLength,
                Array.Empty<RockchipBootEntry>());
        }

        if (header.BootSize > int.MaxValue) {
            throw new InvalidDataException("The BOOT image is too large to parse.");
        }

        var data = new byte[checked((int)header.BootSize)];
        StreamRange.ReadExactly(stream, data, header.BootOffset);
        var magic = U32(data, 0);
        if (RockchipNewIdbReader.IsMagic(magic)) {
            using var boot = new MemoryStream(data, writable: false);
            var result = RockchipNewIdbReader.Read(boot, magic);
            return (
                result.Header.Kind,
                result.Header.HeaderSize,
                result.Entries);
        }

        if (magic is not (BootMagic or LegacyBootMagic)) {
            return (
                RockchipImageKind.Boot,
                HeaderLength,
                Array.Empty<RockchipBootEntry>());
        }

        var entryCount = data[25] + data[31] + data[37];
        var entries = new List<RockchipBootEntry>(entryCount);
        foreach (var table in BootTables) {
            ReadTable(data, table, entries);
        }

        return (
            RockchipImageKind.Boot,
            U16(data, 4),
            entries.AsReadOnly());
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
