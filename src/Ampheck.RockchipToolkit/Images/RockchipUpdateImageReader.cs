using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Images;

internal static class RockchipUpdateImageReader {
    private const int FirmwareHeaderLength = 140;
    private const int FirmwareEntryLength = 112;
    private const int MaximumEntryCount = 32;
    private const uint FirmwareTag = 0x46414B52;

    public static (
        RockchipImageHeader ContainerHeader,
        uint Version,
        string MachineModel,
        string Manufacturer,
        IReadOnlyList<RockchipUpdateEntry> Entries) Read(FileStream stream) {
        var container = RockchipImageReader.Read(stream).Header;
        if (container.Kind != RockchipImageKind.Firmware) {
            throw new InvalidDataException(
                "The file is a BOOT image, not a Rockchip update image.");
        }

        if (container.FirmwareSize < FirmwareHeaderLength) {
            throw new InvalidDataException(
                "The RKAF firmware header is truncated.");
        }

        var header = new byte[FirmwareHeaderLength];
        StreamRange.ReadExactly(
            stream,
            header,
            container.FirmwareOffset);
        if (U32(header, 0) != FirmwareTag) {
            throw new InvalidDataException(
                "The RKFW payload has no RKAF firmware signature.");
        }

        var headerSize = U32(header, 4);
        var entryCount = BinaryPrimitives.ReadInt32LittleEndian(
            header.AsSpan(136));
        if (headerSize < FirmwareHeaderLength ||
            entryCount < 0 ||
            entryCount > MaximumEntryCount) {
            throw new InvalidDataException(
                $"The RKAF header declares {entryCount} entries.");
        }

        var entryData = new byte[checked(
            entryCount * FirmwareEntryLength)];
        StreamRange.ReadExactly(
            stream,
            entryData,
            checked(container.FirmwareOffset + FirmwareHeaderLength));
        var entries = ReadEntries(
            stream,
            container,
            entryData,
            entryCount);

        return (
            container,
            U32(header, 132),
            DecodeText(header.AsSpan(8, 64)),
            DecodeText(header.AsSpan(72, 60)),
            entries);
    }

    private static IReadOnlyList<RockchipUpdateEntry> ReadEntries(
        FileStream stream,
        RockchipImageHeader container,
        byte[] data,
        int entryCount) {
        var entries = new List<RockchipUpdateEntry>(entryCount);
        for (var index = 0; index < entryCount; index++) {
            var offset = index * FirmwareEntryLength;
            var name = DecodeText(data.AsSpan(offset, 32));
            var fileName = DecodeText(data.AsSpan(offset + 32, 64));
            var rawOffset = U32(data, offset + 96);
            var flashOffset = U32(data, offset + 100);
            var useSpace = U32(data, offset + 104);
            var size = U32(data, offset + 108);
            var absoluteOffset = ResolveAbsoluteOffset(
                stream,
                container,
                name,
                rawOffset,
                size);
            entries.Add(new RockchipUpdateEntry(
                name,
                fileName,
                absoluteOffset,
                flashOffset,
                useSpace,
                size));
        }

        var duplicate = entries
            .GroupBy(
                entry => entry.Name,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) {
            throw new InvalidDataException(
                $"The update image contains duplicate entry '{duplicate.Key}'.");
        }

        return entries.AsReadOnly();
    }

    private static long ResolveAbsoluteOffset(
        FileStream stream,
        RockchipImageHeader container,
        string name,
        uint rawOffset,
        uint size) {
        if (size == 0) {
            return rawOffset;
        }

        if (name.Equals(
                "bootloader",
                StringComparison.OrdinalIgnoreCase) &&
            size == container.BootSize &&
            (long)container.BootOffset <= stream.Length - size) {
            return container.BootOffset;
        }

        var relativeOffset = checked(
            (long)container.FirmwareOffset + rawOffset);
        if (rawOffset <= container.FirmwareSize - size &&
            relativeOffset <= stream.Length - size) {
            return relativeOffset;
        }

        throw new InvalidDataException(
            $"Update entry '{name}' points outside the image.");
    }

    private static string DecodeText(ReadOnlySpan<byte> data) {
        var terminator = data.IndexOf((byte)0);
        if (terminator >= 0) {
            data = data[..terminator];
        }

        return Encoding.ASCII.GetString(data)
            .Trim();
    }

    private static uint U32(ReadOnlySpan<byte> data, int offset) {
        return BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }
}
