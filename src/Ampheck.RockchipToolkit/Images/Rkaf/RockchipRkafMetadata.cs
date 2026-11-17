using System.Globalization;
using System.Text;
using Ampheck.RockchipToolkit.Images.Update;

namespace Ampheck.RockchipToolkit.Images.Rkaf;

internal sealed record RockchipRkafMetadata(
    uint Version,
    string MachineModel,
    string MachineId,
    string Manufacturer,
    IReadOnlyDictionary<string, RockchipRkafMetadataEntry> Entries
) {
    public const string FileName = "rkaf-metadata.tsv";

    public static async Task WriteAsync(
        string directory,
        RockchipUpdateImage update,
        CancellationToken cancellationToken
    ) {
        var path = Path.Combine(directory, FileName);
        if (File.Exists(path)) {
            throw new InvalidDataException(
                $"Cannot write '{FileName}' because the image contains that path.");
        }

        var lines = new List<string> {
            $"version\t0x{update.Version:x8}",
            $"model\t{update.MachineModel}",
            $"machine-id\t{update.MachineId}",
            $"manufacturer\t{update.Manufacturer}"
        };
        foreach (var entry in update.Entries) {
            lines.Add(string.Join(
                '\t',
                "entry",
                entry.Name,
                $"0x{entry.FlashSize:x8}",
                $"0x{entry.FlashOffset:x8}"));
        }

        await File.WriteAllLinesAsync(
                path,
                lines,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<RockchipRkafMetadata?> ReadAsync(
        string directory,
        CancellationToken cancellationToken
    ) {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path)) {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, RockchipRkafMetadataEntry>(
            StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < lines.Length; index++) {
            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) {
                continue;
            }

            var fields = line.Split('\t', StringSplitOptions.None);
            if (fields[0] == "entry") {
                ParseEntry(fields, entries, index + 1);
                continue;
            }

            if (fields.Length != 2 || !values.TryAdd(fields[0], fields[1])) {
                throw InvalidLine(index + 1);
            }
        }

        return new RockchipRkafMetadata(
            ParseUInt(ReadRequired(values, "version"), "version"),
            ReadRequired(values, "model"),
            ReadRequired(values, "machine-id"),
            ReadRequired(values, "manufacturer"),
            entries);
    }

    private static void ParseEntry(
        string[] fields,
        IDictionary<string, RockchipRkafMetadataEntry> entries,
        int lineNumber
    ) {
        if (fields.Length != 4) {
            throw InvalidLine(lineNumber);
        }

        var name = fields[1];
        if (!entries.TryAdd(
                name,
                new RockchipRkafMetadataEntry(
                    ParseUInt(fields[2], "flash size"),
                    ParseUInt(fields[3], "flash offset")))) {
            throw new InvalidDataException(
                $"Duplicate entry in {FileName} at line {lineNumber}.");
        }
    }

    private static string ReadRequired(
        IReadOnlyDictionary<string, string> values,
        string name
    ) {
        return values.TryGetValue(name, out var value)
            ? value
            : throw new InvalidDataException(
                $"{FileName} has no '{name}' field.");
    }

    private static uint ParseUInt(string value, string description) {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(
                value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var hex)) {
            return hex;
        }

        if (uint.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number)) {
            return number;
        }

        throw new InvalidDataException(
            $"Invalid {description} '{value}' in {FileName}.");
    }

    private static InvalidDataException InvalidLine(int lineNumber) {
        return new InvalidDataException(
            $"Invalid {FileName} line {lineNumber}.");
    }
}
