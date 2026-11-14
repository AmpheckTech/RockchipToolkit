using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Images;

internal sealed record RockchipUpdateImageReadResult(
    RockchipUpdateImageKind Kind,
    RockchipImageHeader? ContainerHeader,
    uint Version,
    string MachineModel,
    string MachineId,
    string Manufacturer,
    IReadOnlyList<RockchipUpdateEntry> Entries);

internal static class RockchipUpdateImageReader {
    private const uint FirmwareTag = 0x46414B52;
    private const uint ImageTag = 0x57464B52;

    public static RockchipUpdateImageReadResult Read(Stream stream) {
        Span<byte> signature = stackalloc byte[4];
        StreamRange.ReadExactly(stream, signature, 0);
        var tag = BinaryPrimitives.ReadUInt32LittleEndian(signature);

        RockchipUpdateImageKind kind;
        RockchipImageHeader? container;
        long firmwareOffset;
        long firmwareSize;
        switch (tag) {
            case FirmwareTag:
                kind = RockchipUpdateImageKind.Rkaf;
                container = null;
                firmwareOffset = 0;
                firmwareSize = stream.Length;
                break;
            case ImageTag:
                kind = RockchipUpdateImageKind.Rkfw;
                container = RockchipImageReader.Read(stream).Header;
                if (container.Kind != RockchipImageKind.Firmware) {
                    throw new InvalidDataException(
                        "The file does not contain a Rockchip update image.");
                }

                firmwareOffset = container.FirmwareOffset;
                firmwareSize = container.FirmwareSize;
                break;
            default:
                throw new InvalidDataException(
                    "The file is neither an RKFW nor an RKAF update image.");
        }

        if (firmwareSize < RockchipImageLayout.RkafHeaderSize) {
            throw new InvalidDataException(
                "The RKAF firmware header is truncated.");
        }

        var header = new byte[RockchipImageLayout.RkafHeaderSize];
        StreamRange.ReadExactly(stream, header, firmwareOffset);
        if (U32(header, 0) != FirmwareTag) {
            throw new InvalidDataException(
                "The update image has no RKAF firmware signature.");
        }

        var entryCount = U32(header, RockchipImageLayout.RkafEntryCountOffset);
        if (entryCount > RockchipImageLayout.RkafMaximumEntries) {
            throw new InvalidDataException(
                $"The RKAF header declares {entryCount} entries.");
        }

        var entries = ReadEntries(
            header,
            container,
            firmwareOffset,
            firmwareSize,
            checked((int)entryCount));
        return new RockchipUpdateImageReadResult(
            kind,
            container,
            U32(header, RockchipImageLayout.RkafVersionOffset),
            DecodeText(header.AsSpan(
                RockchipImageLayout.RkafMachineModelOffset,
                RockchipImageLayout.RkafMachineModelSize)),
            DecodeText(header.AsSpan(
                RockchipImageLayout.RkafMachineIdOffset,
                RockchipImageLayout.RkafMachineIdSize)),
            DecodeText(header.AsSpan(
                RockchipImageLayout.RkafManufacturerOffset,
                RockchipImageLayout.RkafManufacturerSize)),
            entries);
    }

    private static IReadOnlyList<RockchipUpdateEntry> ReadEntries(
        ReadOnlySpan<byte> header,
        RockchipImageHeader? container,
        long firmwareOffset,
        long firmwareSize,
        int entryCount) {
        var entries = new List<RockchipUpdateEntry>(entryCount);
        var knownMembers = new List<(
            uint StoredOffset,
            string Path,
            long Offset,
            long Size)>();
        var previousAllocationEnd = (long)RockchipImageLayout.RkafHeaderSize;
        var firmwareEnd = firmwareOffset + firmwareSize -
            RockchipImageLayout.RkafChecksumSize;

        for (var index = 0; index < entryCount; index++) {
            var position = RockchipImageLayout.RkafEntryOffset +
                (index * RockchipImageLayout.RkafEntrySize);

            // RKAF entry layout is little-endian and fixed at 112 bytes:
            // name +0x00 (32), path +0x20 (60), flash size +0x5c,
            // part offset +0x60, flash offset +0x64, padded sectors +0x68,
            // and stored byte count +0x6c.
            var name = DecodeText(header.Slice(
                position,
                RockchipImageLayout.RkafEntryNameSize));
            var path = DecodeText(header.Slice(
                position + RockchipImageLayout.RkafEntryPathOffset,
                RockchipImageLayout.RkafEntryPathSize));
            var flashSize = U32(
                header,
                position + RockchipImageLayout.RkafEntryFlashSizeOffset);
            var storedOffset = U32(
                header,
                position + RockchipImageLayout.RkafEntryStoredOffsetOffset);
            var flashOffset = U32(
                header,
                position + RockchipImageLayout.RkafEntryFlashOffsetOffset);
            var paddedSectors = U32(
                header,
                position + RockchipImageLayout.RkafEntryPaddedSectorsOffset);
            var storedSize = U32(
                header,
                position + RockchipImageLayout.RkafEntryStoredSizeOffset);

            long absoluteOffset;
            long size;
            if (container is not null &&
                name.Equals("bootloader", StringComparison.OrdinalIgnoreCase) &&
                storedSize == container.BootSize) {
                absoluteOffset = container.BootOffset;
                size = container.BootSize;
            } else {
                var known = knownMembers.FirstOrDefault(member =>
                    member.StoredOffset == storedOffset &&
                    member.Path.Equals(path, StringComparison.Ordinal));
                if (known.Path is not null) {
                    absoluteOffset = known.Offset;
                    size = known.Size;
                } else {
                    var paddedBytes = checked(
                        (long)paddedSectors * RockchipImageLayout.SectorSize);
                    size = paddedBytes == 0
                        ? storedSize
                        : RockchipImageLayout.RecoverUInt32Value(
                            storedSize,
                            paddedBytes);
                    long offset;
                    if (storedOffset == 0 && size == 0) {
                        offset = 0;
                    } else {
                        offset = storedOffset;
                        if (offset < previousAllocationEnd) {
                            var wraps = (previousAllocationEnd - offset +
                                RockchipImageLayout.UInt32Range - 1) /
                                RockchipImageLayout.UInt32Range;
                            offset = checked(offset +
                                (wraps * RockchipImageLayout.UInt32Range));
                        }

                        var allocationSize = paddedBytes == 0
                            ? RockchipImageLayout.AlignUp(
                                size,
                                RockchipImageLayout.SectorSize)
                            : paddedBytes;
                        previousAllocationEnd = checked(offset + allocationSize);
                    }

                    absoluteOffset = firmwareOffset + offset;
                    knownMembers.Add((
                        storedOffset,
                        path,
                        absoluteOffset,
                        size));
                }

                if (size > 0) {
                    var relativeOffset = absoluteOffset - firmwareOffset;
                    if (relativeOffset < RockchipImageLayout.RkafHeaderSize ||
                        relativeOffset > firmwareSize ||
                        size > firmwareSize - relativeOffset) {
                        throw new InvalidDataException(
                            $"Update entry '{name}' points outside the RKAF image.");
                    }

                    if (absoluteOffset + size > firmwareEnd) {
                        throw new InvalidDataException(
                            $"Update entry '{name}' overlaps the RKAF checksum.");
                    }
                }
            }

            entries.Add(new RockchipUpdateEntry(
                name,
                path,
                absoluteOffset,
                flashOffset,
                flashSize,
                paddedSectors,
                size));
        }

        var duplicate = entries
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) {
            throw new InvalidDataException(
                $"The update image contains duplicate entry '{duplicate.Key}'.");
        }

        return entries.AsReadOnly();
    }

    private static string DecodeText(ReadOnlySpan<byte> data) {
        var terminator = data.IndexOf((byte)0);
        if (terminator >= 0) {
            data = data[..terminator];
        }

        return Encoding.ASCII.GetString(data).Trim();
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) {
        return BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }
}
