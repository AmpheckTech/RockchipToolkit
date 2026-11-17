using System.Buffers.Binary;
using System.Text;
using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Images.Rkaf;

internal static class RockchipRkafHeader {
    private const uint FirmwareTag = 0x46414B52;

    public static byte[] Create(
        long imageLength,
        RockchipParameterInfo? parameter,
        RockchipRkafMetadata? metadata,
        IReadOnlyList<RockchipRkafHeaderEntry> entries
    ) {
        if (entries.Count is 0 or > RockchipImageLayout.RkafMaximumEntries) {
            throw new InvalidDataException(
                $"RKAF requires 1 to {RockchipImageLayout.RkafMaximumEntries} entries.");
        }

        var header = new byte[RockchipImageLayout.RkafHeaderSize];
        var partitions = parameter?.Partitions.ToDictionary(
                partition => partition.Name,
                StringComparer.OrdinalIgnoreCase) ??
            new Dictionary<string, RockchipPartition>(
                StringComparer.OrdinalIgnoreCase);
        // afptool uses the lowest flash offset among matched non-parameter
        // entries for the parameter entry; unmatched package entries do not
        // contribute.
        var parameterFlashSize = 0u;
        var hasParameterFlashOffset = false;
        foreach (var entry in entries) {
            if (entry.Name.Equals(
                    "parameter",
                    StringComparison.OrdinalIgnoreCase) ||
                !partitions.TryGetValue(entry.Name, out var partition)) {
                continue;
            }

            if (!hasParameterFlashOffset ||
                partition.Offset < parameterFlashSize) {
                parameterFlashSize = partition.Offset;
                hasParameterFlashOffset = true;
            }
        }

        BinaryPrimitives.WriteUInt32LittleEndian(header, FirmwareTag);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkafLengthOffset),
            unchecked((uint)imageLength));
        WriteString(
            header.AsSpan(
                RockchipImageLayout.RkafMachineModelOffset,
                RockchipImageLayout.RkafMachineModelSize),
            PrefixValue(
                parameter?.MachineModel ??
                    metadata?.MachineModel ??
                    string.Empty),
            "RKAF machine model");
        WriteString(
            header.AsSpan(
                RockchipImageLayout.RkafMachineIdOffset,
                RockchipImageLayout.RkafMachineIdSize),
            PrefixValue(
                parameter?.MachineId ??
                    metadata?.MachineId ??
                    string.Empty),
            "RKAF machine ID");
        WriteString(
            header.AsSpan(
                RockchipImageLayout.RkafManufacturerOffset,
                RockchipImageLayout.RkafManufacturerSize),
            PrefixValue(
                parameter?.Manufacturer ??
                    metadata?.Manufacturer ??
                    string.Empty),
            "RKAF manufacturer");
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkafVersionOffset),
            parameter?.FirmwareVersion ?? metadata?.Version ?? 0);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkafEntryCountOffset),
            checked((uint)entries.Count));

        for (var index = 0; index < entries.Count; index++) {
            WriteEntry(
                header.AsSpan(
                    RockchipImageLayout.RkafEntryOffset +
                        (index * RockchipImageLayout.RkafEntrySize),
                    RockchipImageLayout.RkafEntrySize),
                entries[index],
                parameter,
                partitions,
                parameterFlashSize,
                metadata);
        }

        return header;
    }

    private static void WriteEntry(
        Span<byte> entry,
        RockchipRkafHeaderEntry source,
        RockchipParameterInfo? parameter,
        IReadOnlyDictionary<string, RockchipPartition> partitions,
        uint parameterFlashSize,
        RockchipRkafMetadata? metadata
    ) {
        WriteString(
            entry[..RockchipImageLayout.RkafEntryNameSize],
            source.Name,
            "RKAF entry name");
        WriteString(
            entry.Slice(
                RockchipImageLayout.RkafEntryPathOffset,
                RockchipImageLayout.RkafEntryPathSize),
            source.FileName,
            "RKAF entry path");

        uint flashSize;
        uint flashOffset;
        if (parameter is not null &&
            source.Name.Equals(
                "parameter",
                StringComparison.OrdinalIgnoreCase)) {
            flashSize = parameterFlashSize;
            flashOffset = 0;
        } else if (parameter is not null &&
            partitions.TryGetValue(source.Name, out var partition)) {
            flashSize = partition.Size ?? uint.MaxValue;
            flashOffset = partition.Offset;
        } else if (metadata?.Entries.TryGetValue(
                source.Name,
                out var preserved) == true) {
            flashSize = preserved.FlashSize;
            flashOffset = preserved.FlashOffset;
        } else {
            flashSize = 0;
            flashOffset = uint.MaxValue;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(
            entry[RockchipImageLayout.RkafEntryFlashSizeOffset..],
            flashSize);
        BinaryPrimitives.WriteUInt32LittleEndian(
            entry[RockchipImageLayout.RkafEntryStoredOffsetOffset..],
            unchecked((uint)source.Offset));
        BinaryPrimitives.WriteUInt32LittleEndian(
            entry[RockchipImageLayout.RkafEntryFlashOffsetOffset..],
            flashOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            entry[RockchipImageLayout.RkafEntryPaddedSectorsOffset..],
            checked((uint)(source.PaddedSize / RockchipImageLayout.SectorSize)));
        BinaryPrimitives.WriteUInt32LittleEndian(
            entry[RockchipImageLayout.RkafEntryStoredSizeOffset..],
            unchecked((uint)source.Size));
    }

    private static string PrefixValue(string value) {
        // afptool writes a leading space before non-empty RKAF text fields.
        return value.Length == 0 ? value : $" {value}";
    }

    private static void WriteString(
        Span<byte> field,
        string value,
        string description
    ) {
        if (value.Length == 0) {
            return;
        }

        if (value.Any(character => character > 0x7f || char.IsControl(character))) {
            throw new InvalidDataException(
                $"The {description} must be printable ASCII.");
        }

        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length >= field.Length) {
            throw new InvalidDataException(
                $"The {description} is too long for its {field.Length - 1}-byte field.");
        }

        bytes.CopyTo(field);
    }
}
