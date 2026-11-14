using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Images;

internal static class RockchipUpdatePackage {
    private const int CopyBufferSize = 4 * 1024 * 1024;

    public static async Task WriteAsync(
        string inputDirectory,
        string outputPath,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken) {
        var packagePath = Path.Combine(inputDirectory, RockchipPackageFile.FileName);
        if (!File.Exists(packagePath)) {
            throw new FileNotFoundException(
                "package-file is required to build an RKAF image.",
                packagePath);
        }

        // Comments may be GBK; entry names and paths are ASCII.
        var packageText = Encoding.UTF8.GetString(
            await File.ReadAllBytesAsync(packagePath, cancellationToken)
                .ConfigureAwait(false));
        if (packageText.Length > 0 && packageText[0] == '\uFEFF') {
            packageText = packageText[1..];
        }

        var packageLines = packageText.Split('\n');
        var entries = new List<RockchipRkafHeaderEntry>();
        var layouts = new List<LayoutEntry>();
        var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var layoutByPath = new Dictionary<string, LayoutEntry>(
            StringComparer.Ordinal);
        var parameterText = (string?)null;
        var currentOffset = (long)RockchipImageLayout.RkafHeaderSize;

        for (var lineIndex = 0; lineIndex < packageLines.Length; lineIndex++) {
            cancellationToken.ThrowIfCancellationRequested();
            var line = packageLines[lineIndex].Trim();
            if (line.Length == 0 || line.StartsWith('#')) {
                continue;
            }

            var fields = line.Split(
                [' ', '\t'],
                StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2) {
                throw new InvalidDataException(
                    $"package-file line {lineIndex + 1} must contain an " +
                    "entry name and file path.");
            }

            var name = fields[0];
            var memberPath = fields[1];
            if (memberPath.Equals(
                    "RESERVED",
                    StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            ValidatePackageToken(name, "entry name");
            ValidatePackageToken(memberPath, "entry path");
            if (!entryNames.Add(name)) {
                throw new InvalidDataException(
                    $"package-file contains duplicate entry '{name}'.");
            }

            if (entries.Count >= RockchipImageLayout.RkafMaximumEntries) {
                throw new InvalidDataException(
                    $"package-file contains more than " +
                    $"{RockchipImageLayout.RkafMaximumEntries} entries.");
            }

            if (memberPath.Equals(
                    "SELF",
                    StringComparison.OrdinalIgnoreCase)) {
                entries.Add(new RockchipRkafHeaderEntry(
                    name,
                    memberPath,
                    Offset: 0,
                    Size: 0,
                    PaddedSize: 0));
                continue;
            }

            byte[]? parameterContent = null;
            var source = ResolveInputPath(inputDirectory, memberPath);
            if (name.Equals(
                    "parameter",
                    StringComparison.OrdinalIgnoreCase)) {
                parameterContent = await File.ReadAllBytesAsync(
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                parameterText = RockchipParm.TryUnwrap(
                        parameterContent,
                        out var parameterBytes)
                    ? Encoding.UTF8.GetString(parameterBytes)
                    : Encoding.UTF8.GetString(parameterContent);
                parameterText = parameterText.TrimEnd('\0');
            }

            if (layoutByPath.TryGetValue(memberPath, out var existingLayout)) {
                entries.Add(new RockchipRkafHeaderEntry(
                    name,
                    memberPath,
                    existingLayout.Offset,
                    existingLayout.Size,
                    existingLayout.PaddedSize));
                continue;
            }

            byte[]? prebuilt = null;
            long size;
            if (parameterContent is not null) {
                prebuilt = RockchipParm.TryUnwrap(
                        parameterContent,
                        out _)
                    ? parameterContent
                    : RockchipParm.Wrap(parameterContent);
                size = prebuilt.Length;
            } else {
                size = new FileInfo(source).Length;
            }

            var paddedSize = RockchipImageLayout.AlignUp(
                size,
                RockchipImageLayout.SectorSize);
            var layout = new LayoutEntry(
                source,
                prebuilt,
                size,
                currentOffset,
                paddedSize);
            layouts.Add(layout);
            layoutByPath.Add(memberPath, layout);
            entries.Add(new RockchipRkafHeaderEntry(
                name,
                memberPath,
                layout.Offset,
                layout.Size,
                layout.PaddedSize));
            currentOffset = checked(currentOffset + paddedSize);
        }

        if (entries.Count == 0) {
            throw new InvalidDataException(
                "package-file contains no image entries.");
        }

        RockchipParameterInfo? parameter = null;
        RockchipRkafMetadata? metadata = null;
        if (parameterText is null) {
            metadata = await RockchipRkafMetadata.ReadAsync(
                    inputDirectory,
                    cancellationToken)
                .ConfigureAwait(false);
        } else {
            parameter = RockchipParameterInfo.Parse(parameterText);
        }

        var header = RockchipRkafHeader.Create(
            currentOffset,
            parameter,
            metadata,
            entries);

        progress.AddTotal(checked(
            currentOffset +
            RockchipImageLayout.RkafChecksumSize));
        await using var output = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[CopyBufferSize];
        var crc = RockchipCrc32.Append(0, header);
        await output.WriteAsync(header, cancellationToken)
            .ConfigureAwait(false);
        progress.Advance(header.Length);

        foreach (var layout in layouts) {
            crc = await WriteLayoutAsync(
                    output,
                    layout,
                    buffer,
                    crc,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var checksum = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(
            checksum,
            crc);
        await output.WriteAsync(checksum, cancellationToken)
            .ConfigureAwait(false);
        progress.Advance(checksum.Length);
    }

    private static async Task<uint> WriteLayoutAsync(
        FileStream output,
        LayoutEntry layout,
        byte[] buffer,
        uint crc,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken) {
        if (layout.Prebuilt is not null) {
            crc = RockchipCrc32.Append(crc, layout.Prebuilt);
            await output.WriteAsync(
                    layout.Prebuilt,
                    cancellationToken)
                .ConfigureAwait(false);
            progress.Advance(layout.Prebuilt.Length);
        } else {
            await using var source = new FileStream(
                layout.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var remaining = layout.Size;
            while (remaining > 0) {
                var requested = (int)Math.Min(buffer.Length, remaining);
                var read = await source.ReadAsync(
                        buffer.AsMemory(0, requested),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    throw new IOException(
                        $"'{layout.SourcePath}' changed size while packing.");
                }

                crc = RockchipCrc32.Append(
                    crc,
                    buffer.AsSpan(0, read));
                await output.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                progress.Advance(read);
                remaining -= read;
            }

            if (source.ReadByte() != -1) {
                throw new IOException(
                    $"'{layout.SourcePath}' changed size while packing.");
            }
        }

        var padding = checked((int)(layout.PaddedSize - layout.Size));
        if (padding > 0) {
            Array.Clear(buffer, 0, padding);
            crc = RockchipCrc32.Append(
                crc,
                buffer.AsSpan(0, padding));
            await output.WriteAsync(
                    buffer.AsMemory(0, padding),
                    cancellationToken)
                .ConfigureAwait(false);
            progress.Advance(padding);
        }

        return crc;
    }

    private static void ValidatePackageToken(
        string value,
        string description) {
        if (string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace)) {
            throw new InvalidDataException(
                $"The {description} '{value}' is not representable in package-file.");
        }
    }

    private static string ResolveInputPath(
        string inputDirectory,
        string packagePath) {
        var path = ResolvePath(inputDirectory, packagePath);
        if (File.Exists(path)) {
            return path;
        }

        throw new FileNotFoundException(
            $"Cannot find package file '{packagePath}'.",
            path);
    }

    private static string ResolvePath(
        string inputDirectory,
        string relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath)) {
            throw new InvalidDataException(
                $"Invalid package path '{relativePath}'.");
        }

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(segment =>
                segment is "." or ".." ||
                segment.Contains(':') ||
                segment.Contains('\0'))) {
            throw new InvalidDataException(
                $"Invalid package path '{relativePath}'.");
        }

        var root = Path.GetFullPath(inputDirectory);
        var result = Path.GetFullPath(Path.Combine([root, .. segments]));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!result.StartsWith(
                prefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)) {
            throw new InvalidDataException(
                $"Package path '{relativePath}' escapes the input directory.");
        }

        return result;
    }

    private sealed record LayoutEntry(
        string SourcePath,
        byte[]? Prebuilt,
        long Size,
        long Offset,
        long PaddedSize);
}
