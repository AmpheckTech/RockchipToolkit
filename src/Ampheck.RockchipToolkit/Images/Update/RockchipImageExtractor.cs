using Ampheck.RockchipToolkit.Images.Common;
using Ampheck.RockchipToolkit.Images.Rkaf;

namespace Ampheck.RockchipToolkit.Images.Update;

/// <summary>Unpacks RKFW or standalone RKAF images.</summary>
public static class RockchipImageExtractor {
    private const int CopyBufferSize = 4 * 1024 * 1024;

    public static Task UnpackAsync(
        string inputPath,
        string outputPath,
        bool overwrite,
        CancellationToken cancellationToken
    ) {
        return UnpackAsync(
            inputPath,
            outputPath,
            overwrite,
            progress: null,
            cancellationToken);
    }

    public static async Task UnpackAsync(
        string inputPath,
        string outputPath,
        bool overwrite,
        IProgress<RockchipImageProgress>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var input = Path.GetFullPath(inputPath);
        var output = Path.GetFullPath(outputPath);
        if (!File.Exists(input)) {
            throw new FileNotFoundException(
                "The image file does not exist.",
                input);
        }

        if (IsWithin(output, input)) {
            throw new IOException(
                "The output directory cannot contain the input image.");
        }

        if (Directory.Exists(output) || File.Exists(output)) {
            if (!overwrite) {
                throw new IOException(
                    $"Output '{output}' already exists. Use --force to replace it.");
            }
        }

        var parent = Path.GetDirectoryName(output) ??
            throw new ArgumentException(
                "The output path has no parent directory.",
                nameof(outputPath));
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(
            parent,
            $".{Path.GetFileName(output)}.tmp-{Guid.NewGuid():N}");
        var reporter = new RockchipImageProgressReporter(progress);

        try {
            using var update = RockchipUpdateImage.Open(input);
            Directory.CreateDirectory(staging);
            await UnpackAsync(
                    input,
                    update,
                    staging,
                    reporter,
                    cancellationToken)
                .ConfigureAwait(false);

            if (Directory.Exists(output)) {
                Directory.Delete(output, recursive: true);
            } else if (File.Exists(output)) {
                File.Delete(output);
            }

            Directory.Move(staging, output);
            reporter.Complete();
        } catch {
            if (Directory.Exists(staging)) {
                Directory.Delete(staging, recursive: true);
            }

            throw;
        }
    }

    private static async Task UnpackAsync(
        string path,
        RockchipUpdateImage update,
        string outputDirectory,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        var extraction = CreateExtractionPlan(
            outputDirectory,
            update.Entries);
        var extractedBytes = extraction.Aggregate(
            0L,
            (total, item) => checked(total + item.Entry.Size));
        var bootSize = update.Kind == RockchipUpdateImageKind.Rkfw
            ? update.ContainerHeader!.BootSize
            : 0;
        progress.AddTotal(checked(bootSize + extractedBytes));

        if (update.Kind == RockchipUpdateImageKind.Rkfw) {
            await using var source = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var container = update.ContainerHeader!;
            await CopyRangeAsync(
                    source,
                    container.BootOffset,
                    container.BootSize,
                    Path.Combine(outputDirectory, "BOOT"),
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await RockchipImageChecksum.ValidateAsync(
                path,
                update,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var item in extraction) {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = item.Entry;
            var destination = item.Destination;

            Directory.CreateDirectory(
                Path.GetDirectoryName(destination)!);
            if (entry.Name.Equals(
                    "parameter",
                    StringComparison.OrdinalIgnoreCase)) {
                var parameter = update.ReadParameterBytes(entry);
                await File.WriteAllBytesAsync(
                        destination,
                        parameter,
                        cancellationToken)
                    .ConfigureAwait(false);
                progress.Advance(entry.Size);
                continue;
            }

            await using var entryStream = update.OpenEntryStream(entry);
            await using var output = CreateOutput(destination);
            await CopyExactlyAsync(
                    entryStream,
                    output,
                    entry.Size,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await RockchipPackageFile.EnsureAsync(
                outputDirectory,
                update.Entries,
                cancellationToken)
            .ConfigureAwait(false);

        if (!update.Entries.Any(entry => entry.Name.Equals(
                "parameter",
                StringComparison.OrdinalIgnoreCase))) {
            await RockchipRkafMetadata.WriteAsync(
                    outputDirectory,
                    update,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<ExtractionEntry> CreateExtractionPlan(
        string outputDirectory,
        IEnumerable<RockchipUpdateEntry> entries
    ) {
        var writtenPaths = new HashSet<string>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
        var extraction = new List<ExtractionEntry>();
        foreach (var entry in entries) {
            if (entry.FileName.Equals(
                    "SELF",
                    StringComparison.OrdinalIgnoreCase) ||
                entry.FileName.Equals(
                    "RESERVED",
                    StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var destination = ResolveEntryPath(
                outputDirectory,
                entry.FileName);
            if (writtenPaths.Add(destination)) {
                extraction.Add(new ExtractionEntry(
                    entry,
                    destination));
            }
        }

        return extraction;
    }

    private static Stream CreateOutput(string path) {
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static string ResolveEntryPath(
        string outputDirectory,
        string entryPath
    ) {
        if (string.IsNullOrWhiteSpace(entryPath) ||
            Path.IsPathRooted(entryPath)) {
            throw new InvalidDataException(
                $"Invalid RKAF entry path '{entryPath}'.");
        }

        var segments = entryPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(segment =>
                segment is "." or ".." ||
                segment.Contains(':') ||
                segment.Contains('\0'))) {
            throw new InvalidDataException(
                $"Invalid RKAF entry path '{entryPath}'.");
        }

        var root = Path.GetFullPath(outputDirectory);
        var result = Path.GetFullPath(
            Path.Combine([root, .. segments]));
        var prefix = root.EndsWith(
            Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!result.StartsWith(
                prefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)) {
            throw new InvalidDataException(
                $"RKAF entry path '{entryPath}' escapes the output directory.");
        }

        return result;
    }

    private static bool IsWithin(
        string directory,
        string path
    ) {
        var relative = Path.GetRelativePath(directory, path);
        return relative == "." ||
            (!Path.IsPathRooted(relative) &&
                relative != ".." &&
                !relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));
    }

    private static async Task CopyRangeAsync(
        FileStream source,
        long offset,
        long length,
        string destination,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        StreamRange.Validate(source.Length, offset, length, "image");
        source.Position = offset;
        await using var output = CreateOutput(destination);
        await CopyExactlyAsync(
                source,
                output,
                length,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CopyExactlyAsync(
        Stream source,
        Stream destination,
        long length,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        var buffer = new byte[CopyBufferSize];
        var remaining = length;
        while (remaining > 0) {
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = await source.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException(
                    "The Rockchip image ended unexpectedly.");
            }

            await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken)
                .ConfigureAwait(false);
            progress.Advance(read);
            remaining -= read;
        }
    }
}
