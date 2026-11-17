using System.Text;
using Ampheck.RockchipToolkit.Images.Update;

namespace Ampheck.RockchipToolkit.Images.Rkaf;

internal static class RockchipPackageFile {
    public const string FileName = "package-file";

    public static async Task EnsureAsync(
        string outputDirectory,
        IReadOnlyList<RockchipUpdateEntry> entries,
        CancellationToken cancellationToken
    ) {
        var path = Path.Combine(outputDirectory, FileName);
        if (!File.Exists(path)) {
            await CreateAsync(path, entries, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await NormalizeAsync(path, entries, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CreateAsync(
        string path,
        IReadOnlyList<RockchipUpdateEntry> entries,
        CancellationToken cancellationToken
    ) {
        var lines = entries.Select(entry => {
            if (entry.Name.Any(char.IsWhiteSpace) ||
                entry.FileName.Any(char.IsWhiteSpace)) {
                throw new InvalidDataException(
                    "RKAF entry names and paths cannot contain whitespace.");
            }

            return $"{entry.Name}\t{entry.FileName}";
        });
        await File.WriteAllLinesAsync(
                path,
                lines,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task NormalizeAsync(
        string path,
        IReadOnlyList<RockchipUpdateEntry> entries,
        CancellationToken cancellationToken
    ) {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        var hasBom = bytes.AsSpan().StartsWith(
            new byte[] { 0xef, 0xbb, 0xbf });
        // Comments may be GBK. Latin-1 preserves bytes outside replaced paths.
        var text = Encoding.Latin1.GetString(
            bytes,
            hasBom ? 3 : 0,
            bytes.Length - (hasBom ? 3 : 0));
        var members = entries.ToDictionary(
            entry => entry.Name,
            StringComparer.OrdinalIgnoreCase);
        var lines = text.Split('\n');
        var changed = false;
        for (var index = 0; index < lines.Length; index++) {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) {
                continue;
            }

            var fields = trimmed.Split(
                [' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 ||
                !members.TryGetValue(fields[0], out var entry) ||
                entry.FileName.Equals(
                    fields[1],
                    StringComparison.Ordinal)) {
                continue;
            }

            var nameIndex = line.IndexOf(
                fields[0],
                StringComparison.Ordinal);
            var pathIndex = nameIndex < 0
                ? -1
                : line.IndexOf(
                    fields[1],
                    nameIndex + fields[0].Length,
                    StringComparison.Ordinal);
            if (pathIndex < 0) {
                continue;
            }

            lines[index] = string.Concat(
                line.AsSpan(0, pathIndex),
                entry.FileName,
                line.AsSpan(pathIndex + fields[1].Length));
            changed = true;
        }

        if (!changed) {
            return;
        }

        var content = Encoding.Latin1.GetBytes(string.Join('\n', lines));
        if (hasBom) {
            content = [0xef, 0xbb, 0xbf, .. content];
        }

        await File.WriteAllBytesAsync(path, content, cancellationToken)
            .ConfigureAwait(false);
    }
}
