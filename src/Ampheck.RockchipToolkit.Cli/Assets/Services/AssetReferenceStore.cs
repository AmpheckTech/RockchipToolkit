using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ampheck.RockchipToolkit.Cli.Assets.Model;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal sealed class AssetReferenceStore(
    AssetCachePaths paths
) {
    public async Task SaveAsync(
        string manifestPath,
        AssetManifest manifest,
        CancellationToken cancellationToken
    ) {
        var fullPath = Path.GetFullPath(manifestPath);
        var reference = new AssetManifestReference(
            fullPath,
            manifest.Provider,
            DateTimeOffset.UtcNow,
            [.. manifest.Assets
                .Select(asset => new AssetReferenceEntry(
                    asset.Sha256,
                    asset.Output,
                    asset.Purpose))]);
        var directory = paths.GetReferenceDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(
            directory,
            $"{ReferenceId(fullPath)}.json");
        var temporary = path + ".tmp";
        try {
            var json = JsonSerializer.Serialize(
                reference,
                AssetJsonContext.Default.AssetManifestReference);
            await File.WriteAllTextAsync(
                    temporary,
                    json,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        } finally {
            TryDelete(temporary);
        }
    }

    public async ValueTask<IReadOnlyList<AssetManifestReference>>
        LoadAllAsync(CancellationToken cancellationToken) {
        var directory = paths.GetReferenceDirectory();
        if (!Directory.Exists(directory)) {
            return [];
        }

        var references = new List<AssetManifestReference>();
        foreach (var path in Directory.EnumerateFiles(
            directory,
            "*.json",
            SearchOption.TopDirectoryOnly)) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                var json = await File.ReadAllTextAsync(path, cancellationToken)
                    .ConfigureAwait(false);
                var reference = JsonSerializer.Deserialize(
                        json,
                        AssetJsonContext.Default.AssetManifestReference) ??
                    throw new JsonException("The reference file is empty.");
                references.Add(reference);
            } catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or
                JsonException) {
                throw new InvalidDataException(
                    $"Unable to read asset reference '{path}': " +
                    error.Message,
                    error);
            }
        }

        return [.. references.OrderBy(reference => reference.ManifestPath, StringComparer.Ordinal)];
    }

    public ValueTask<bool> ForgetAsync(
        string manifestPath,
        CancellationToken cancellationToken
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(manifestPath);
        var path = Path.Combine(
            paths.GetReferenceDirectory(),
            $"{ReferenceId(fullPath)}.json");
        if (!File.Exists(path)) {
            return ValueTask.FromResult(false);
        }

        File.Delete(path);
        return ValueTask.FromResult(true);
    }

    private static string ReferenceId(string manifestPath) {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(manifestPath));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
