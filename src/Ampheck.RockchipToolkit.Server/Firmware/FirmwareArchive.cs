using System.IO.Compression;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal static class FirmwareArchive {
    private const int CopyBufferSize = 128 * 1024;

    public static async Task<long> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        ToolkitTransferLimits limits,
        long alreadyExpandedBytes,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(limits);
        if (alreadyExpandedBytes < 0 ||
            alreadyExpandedBytes >= limits.MaximumExpandedBytes) {
            throw new InvalidDataException(
                "The request has exhausted its expanded data budget.");
        }

        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count >
            limits.MaximumArchiveEntries) {
            throw new InvalidDataException(
                $"The archive contains more than " +
                $"{limits.MaximumArchiveEntries} entries.");
        }

        var remainingBudget =
            limits.MaximumExpandedBytes - alreadyExpandedBytes;
        var declaredBytes = GetDeclaredBytes(
            archive,
            limits,
            remainingBudget);
        var root = Path.GetFullPath(destinationDirectory);
        var buffer = new byte[CopyBufferSize];
        long writtenBytes = 0;
        foreach (var entry in archive.Entries) {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Name.Length == 0) {
                Directory.CreateDirectory(
                    ResolveDestination(root, entry));
                continue;
            }

            var destination = ResolveDestination(root, entry);
            Directory.CreateDirectory(
                Path.GetDirectoryName(destination)!);
            await using var source = entry.Open();
            await using var target = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            long entryBytes = 0;
            while (true) {
                var read = await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                if (read > remainingBudget - writtenBytes) {
                    throw new InvalidDataException(
                        "The archive exceeds the configured expanded " +
                        "data limit.");
                }

                if (read > entry.Length - entryBytes) {
                    throw new InvalidDataException(
                        "An archive entry exceeds its declared size.");
                }

                await target.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                writtenBytes += read;
                entryBytes += read;
            }
        }

        if (writtenBytes > declaredBytes) {
            throw new InvalidDataException(
                "The archive expanded beyond its declared size.");
        }

        return writtenBytes;
    }

    public static string FindRoot(string archiveRoot) {
        if (File.Exists(
                Path.Combine(archiveRoot, "parameter.txt"))) {
            return archiveRoot;
        }

        var candidates = Directory
            .EnumerateDirectories(archiveRoot)
            .Where(directory => File.Exists(
                Path.Combine(directory, "parameter.txt")))
            .Take(2)
            .ToArray();
        return candidates.Length == 1
            ? candidates[0]
            : throw new InvalidDataException(
                "The firmware archive contains no unambiguous parameter.txt.");
    }

    private static long GetDeclaredBytes(
        ZipArchive archive,
        ToolkitTransferLimits limits,
        long remainingBudget) {
        long declaredBytes = 0;
        foreach (var entry in archive.Entries) {
            if (entry.FullName.Length >
                limits.MaximumArchivePathLength) {
                throw new InvalidDataException(
                    "The archive contains an overlong entry path.");
            }

            if (entry.Length < 0) {
                throw new InvalidDataException(
                    "The archive contains an invalid entry size.");
            }

            if (entry.Length > remainingBudget - declaredBytes) {
                throw new InvalidDataException(
                    "The archive declares more data than the configured " +
                    "expanded data limit.");
            }

            declaredBytes += entry.Length;
        }

        return declaredBytes;
    }

    private static string ResolveDestination(
        string root,
        ZipArchiveEntry entry) {
        var relative = entry.FullName.Replace(
            '\\',
            Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative) ||
            Path.IsPathRooted(relative)) {
            throw new InvalidDataException(
                "The firmware archive contains an invalid path.");
        }

        var segments = relative.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(segment =>
                segment is "." or ".." ||
                OperatingSystem.IsWindows() &&
                (segment.Contains(':') ||
                    IsWindowsDeviceName(segment)))) {
            throw new InvalidDataException(
                "The firmware archive contains an unsafe path.");
        }

        try {
            var destination = Path.GetFullPath(
                Path.Combine(root, relative));
            var pathFromRoot = Path.GetRelativePath(
                root,
                destination);
            if (Path.IsPathRooted(pathFromRoot) ||
                pathFromRoot == ".." ||
                pathFromRoot.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)) {
                throw new InvalidDataException(
                    "The firmware archive contains an invalid path.");
            }

            return destination;
        } catch (Exception error) when (
            error is ArgumentException or NotSupportedException or
            PathTooLongException) {
            throw new InvalidDataException(
                "The firmware archive contains an invalid path.",
                error);
        }
    }

    private static bool IsWindowsDeviceName(string segment) {
        var name = segment.Split('.')[0];
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            name.Length == 4 &&
            (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            name[3] is >= '1' and <= '9';
    }
}
