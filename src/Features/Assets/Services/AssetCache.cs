using System.Net;
using System.Security.Cryptography;
using Ampheck.RockchipToolkit.Features.Assets.Model;
using Ampheck.RockchipToolkit.Features.Assets.Providers;

namespace Ampheck.RockchipToolkit.Features.Assets.Services;

internal sealed class AssetCache(
    AssetCachePaths paths
) {
    private const int BufferSize = 128 * 1024;
    private static readonly HttpClient HttpClient = new(
        new SocketsHttpHandler {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        }) {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public string Root => paths.Root;

    public string GetAssetPath(string sha256) => paths.GetAssetPath(sha256);

    public async ValueTask<AssetCacheEntry?> EnsureAsync(
        AssetEntry asset,
        IAssetProvider provider,
        bool offline,
        CancellationToken cancellationToken
    ) {
        var path = paths.GetAssetPath(asset.Sha256);
        if (await IsValidAsync(path, asset.Sha256, cancellationToken)
            .ConfigureAwait(false)) {
            return CreateEntry(asset.Sha256, path);
        }

        if (offline) {
            if (File.Exists(path)) {
                throw new AssetIntegrityException(
                    $"Cached asset '{asset.Output}' failed SHA-256 validation.");
            }

            return null;
        }

        await using var cacheLock = await AcquireLockAsync(
                asset.Sha256,
                cancellationToken)
            .ConfigureAwait(false);
        if (await IsValidAsync(path, asset.Sha256, cancellationToken)
            .ConfigureAwait(false)) {
            return CreateEntry(asset.Sha256, path);
        }

        TryDelete(path);
        await DownloadAsync(asset, provider, path, cancellationToken)
            .ConfigureAwait(false);
        return CreateEntry(asset.Sha256, path);
    }

    public async ValueTask<bool> IsValidAsync(
        string path,
        string sha256,
        CancellationToken cancellationToken
    ) {
        if (!File.Exists(path)) {
            return false;
        }

        return string.Equals(
            await AssetHash.ComputeAsync(path, cancellationToken)
                .ConfigureAwait(false),
            sha256,
            StringComparison.Ordinal);
    }

    public async ValueTask<IReadOnlyList<AssetCacheEntry>> ListAsync(
        CancellationToken cancellationToken
    ) {
        var root = Path.Combine(paths.Root, "sha256");
        if (!Directory.Exists(root)) {
            return [];
        }

        var entries = new List<AssetCacheEntry>();
        foreach (var path in Directory.EnumerateFiles(
            root,
            "*",
            SearchOption.AllDirectories)) {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    name,
                    "^[0-9a-f]{64}$")) {
                continue;
            }

            entries.Add(CreateEntry(name, path));
        }

        return entries
            .OrderBy(entry => entry.Sha256, StringComparer.Ordinal)
            .ToArray();
    }

    public void Delete(AssetCacheEntry entry) {
        TryDelete(entry.Path);
        TryDeleteEmptyDirectory(Path.GetDirectoryName(entry.Path));
        if (Path.GetDirectoryName(entry.Path) is { } parent) {
            TryDeleteEmptyDirectory(Path.GetDirectoryName(parent));
        }
    }

    private async Task DownloadAsync(
        AssetEntry asset,
        IAssetProvider provider,
        string path,
        CancellationToken cancellationToken
    ) {
        var directory = Path.GetDirectoryName(path) ??
            throw new InvalidDataException(
                "The asset cache path has no parent directory.");
        Directory.CreateDirectory(directory);
        var partPath = paths.GetPartPath(asset.Sha256);
        try {
            using var response = await HttpClient.GetAsync(
                    provider.Resolve(asset),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                throw new HttpRequestException(
                    $"Download returned HTTP " +
                    $"{(int)response.StatusCode}.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destination = new FileStream(
                partPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
            var buffer = new byte[BufferSize];
            while (true) {
                var read = await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await destination.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            var actual = Convert.ToHexString(
                    hash.GetHashAndReset())
                .ToLowerInvariant();
            if (!string.Equals(
                    actual,
                    asset.Sha256,
                    StringComparison.Ordinal)) {
                throw new AssetIntegrityException(
                    $"SHA-256 mismatch for '{asset.Output}': expected " +
                    $"{asset.Sha256}, got {actual}.");
            }

            File.Move(partPath, path, overwrite: true);
        } catch (HttpRequestException error) {
            throw provider.MapException(asset, error);
        } finally {
            TryDelete(partPath);
        }
    }

    private async ValueTask<FileStream> AcquireLockAsync(
        string sha256,
        CancellationToken cancellationToken
    ) {
        var path = paths.GetLockPath(sha256);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.Asynchronous);
            } catch (IOException) {
                await Task.Delay(
                        100,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static AssetCacheEntry CreateEntry(
        string sha256,
        string path
    ) => new(
        sha256,
        Path.GetFullPath(path),
        new FileInfo(path).Length);

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }

    private static void TryDeleteEmptyDirectory(string? path) {
        if (path is null) {
            return;
        }

        try {
            if (Directory.Exists(path) &&
                !Directory.EnumerateFileSystemEntries(path).Any()) {
                Directory.Delete(path);
            }
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
