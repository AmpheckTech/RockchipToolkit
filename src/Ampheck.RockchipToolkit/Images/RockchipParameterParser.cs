using System.Globalization;

namespace Ampheck.RockchipToolkit.Images;

public static class RockchipParameterParser {
    public static IReadOnlyList<RockchipPartition> Parse(string text) {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var commandLine = text
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                line.StartsWith("CMDLINE:", StringComparison.OrdinalIgnoreCase));
        if (commandLine is null) {
            throw new InvalidDataException(
                "The parameter file contains no CMDLINE entry.");
        }

        var markerIndex = commandLine.IndexOf(
            "mtdparts=",
            StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) {
            throw new InvalidDataException(
                "The parameter CMDLINE contains no mtdparts entry.");
        }

        var definitions = commandLine[(markerIndex + "mtdparts=".Length)..];
        var separatorIndex = definitions.IndexOf(
            ":",
            StringComparison.Ordinal);
        if (separatorIndex < 0) {
            throw new InvalidDataException(
                "The parameter mtdparts entry has no partition list.");
        }

        var partitions = definitions[(separatorIndex + 1)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(partition => partition.Trim())
            .Where(partition => partition.Length > 0)
            .Select(ParsePartition)
            .OrderBy(partition => partition.Offset)
            .ToArray();
        if (partitions.Length == 0) {
            throw new InvalidDataException(
                "The parameter file contains no partitions.");
        }

        if (partitions
                .GroupBy(
                    partition => partition.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1)) {
            throw new InvalidDataException(
                "The parameter file contains duplicate partition names.");
        }

        for (var index = 1; index < partitions.Length; index++) {
            var previous = partitions[index - 1];
            if (previous.Size is { } previousSize &&
                (ulong)previous.Offset + previousSize >
                    partitions[index].Offset) {
                throw new InvalidDataException(
                    $"Partition {previous.Name} overlaps " +
                    $"{partitions[index].Name}.");
            }
        }

        return partitions;
    }

    private static RockchipPartition ParsePartition(string definition) {
        var openIndex = definition.IndexOf(
            "(",
            StringComparison.Ordinal);
        var closeIndex = definition.LastIndexOf(
            ")",
            StringComparison.Ordinal);
        if (openIndex <= 0 || closeIndex <= openIndex) {
            throw InvalidPartition(definition);
        }

        var geometry = definition[..openIndex];
        var nameText = definition[(openIndex + 1)..closeIndex];
        var nameSeparator = nameText.IndexOf(
            ":",
            StringComparison.Ordinal);
        if (nameSeparator >= 0) {
            nameText = nameText[..nameSeparator];
        }

        var offsetSeparator = geometry.IndexOf(
            "@",
            StringComparison.Ordinal);
        if (offsetSeparator <= 0) {
            throw InvalidPartition(definition);
        }

        var sizeText = geometry[..offsetSeparator];
        var offsetText = geometry[(offsetSeparator + 1)..];
        if (string.IsNullOrWhiteSpace(nameText) ||
            !TryParseHex(offsetText, out var offset)) {
            throw InvalidPartition(definition);
        }

        uint? size = null;
        if (!sizeText.Equals("-", StringComparison.Ordinal)) {
            if (!TryParseHex(sizeText, out var parsedSize)) {
                throw InvalidPartition(definition);
            }

            size = parsedSize;
        }

        return new RockchipPartition(nameText, offset, size);
    }

    private static InvalidDataException InvalidPartition(
        string definition) {
        return new InvalidDataException(
            $"Invalid partition definition '{definition}'.");
    }

    private static bool TryParseHex(
        string value,
        out uint result) {
        var digits = value.StartsWith(
            "0x",
            StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;
        return uint.TryParse(
            digits,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out result);
    }

}
