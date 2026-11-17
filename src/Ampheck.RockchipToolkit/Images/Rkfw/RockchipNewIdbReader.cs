using System.Buffers.Binary;
using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Images.Rkfw;

internal static class RockchipNewIdbReader {
    private const int HeaderSize = 2048;
    private const int EntryCountOffset = 0x08;
    private const int FlagsOffset = 0x0C;
    private const int EntryTableOffset = 0x78;
    private const int EntrySize = 0x58;
    private const int SectorSize = 512;
    private const int MaximumEntryCount = 2;
    private const uint RknsMagic = 0x534E4B52;
    private const uint RkssMagic = 0x53534B52;
    private const uint SignedFlag = 1U << 13;

    public static bool IsMagic(uint value) {
        return value is RknsMagic or RkssMagic;
    }

    public static (RockchipImageHeader Header, IReadOnlyList<RockchipBootEntry> Entries
    ) Read(
        Stream stream,
        uint magic) {
        if (!IsMagic(magic)) {
            throw new InvalidDataException("Invalid RKNS or RKSS signature.");
        }

        if (stream.Length < HeaderSize) {
            throw new InvalidDataException("The newidb image is truncated.");
        }

        if (stream.Length > uint.MaxValue) {
            throw new InvalidDataException(
                "Standalone newidb images larger than 4 GiB are not supported.");
        }

        var header = new byte[HeaderSize];
        StreamRange.ReadExactly(stream, header, 0);

        // RKNS and RKSS share struct newidb from Barebox
        // scripts/rockchip.h. 0x08 stores the file count in its high word;
        // 0x0c holds flags. The four 0x58-byte entries start at 0x78.
        var entryCount = U32(header, EntryCountOffset) >> 16;
        if (entryCount > MaximumEntryCount) {
            // The MaskROM bootstrap protocol only exposes CODE471/CODE472.
            throw new InvalidDataException(
                $"Newidb images with {entryCount} USB entries are not supported.");
        }

        var entries = new List<RockchipBootEntry>(checked((int)entryCount));
        for (var index = 0; index < entryCount; index++) {
            var entryOffset = checked(EntryTableOffset + index * EntrySize);
            var sector = U32(header, entryOffset);
            // The low 16 bits are the start sector and the high 16 bits are
            // the sector count. Newidb sectors are 512 bytes.
            var dataOffset = checked((sector & 0xFFFF) * (long)SectorSize);
            var dataSize = checked((sector >> 16) * (long)SectorSize);
            var type = index == 0
                ? BootEntryType.Code471
                : BootEntryType.Code472;
            StreamRange.Validate(
                stream.Length,
                dataOffset,
                dataSize,
                $"newidb {type} entry");
            entries.Add(new RockchipBootEntry(
                type,
                $"Entry {index + 1}",
                checked((uint)dataOffset),
                checked((uint)dataSize),
                0));
        }

        var flags = U32(header, FlagsOffset);
        var kind = magic == RkssMagic
            ? RockchipImageKind.Rkss
            : RockchipImageKind.Rkns;
        var imageHeader = new RockchipImageHeader(
            0,
            0,
            HeaderSize,
            0,
            checked((uint)stream.Length),
            0,
            0,
            0,
            kind == RockchipImageKind.Rkss || (flags & SignedFlag) != 0,
            kind);
        return (imageHeader, entries.AsReadOnly());
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) {
        return BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }
}
