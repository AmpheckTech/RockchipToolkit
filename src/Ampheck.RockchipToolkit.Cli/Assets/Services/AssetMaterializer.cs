using Ampheck.RockchipToolkit.Cli.Assets.Model;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal static class AssetMaterializer {
    public static async Task MaterializeAsync(
        string outputDirectory,
        AssetManifest manifest,
        IReadOnlyDictionary<string, AssetCacheEntry> cached,
        bool useHardLink,
        CancellationToken cancellationToken
    ) {
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        foreach (var asset in manifest.Assets) {
            if (!cached.TryGetValue(asset.Sha256, out var cacheEntry)) {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var destination = AssetPathValidator.ResolveUnderRoot(
                root,
                asset.Output);
            var parent = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(parent);
            var temporary = destination +
                $".{Guid.NewGuid():N}.tmp";
            try {
                if (!useHardLink ||
                    !AssetHardLink.TryCreate(
                        cacheEntry.Path,
                        temporary)) {
                    await CopyAsync(
                            cacheEntry.Path,
                            temporary,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                File.Move(temporary, destination, overwrite: true);
            } finally {
                TryDelete(temporary);
            }
        }
    }

    private static async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken
    ) {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, cancellationToken)
            .ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
