using Ampheck.RockchipToolkit.Cli.Assets.Model;
using Ampheck.RockchipToolkit.Cli.Assets.Providers;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal sealed class AssetManager(
    AssetManifestParser manifests,
    AssetProviderRegistry providers,
    AssetCache cache,
    AssetReferenceStore referenceStore
) {
    public string Root => cache.Root;

    public async ValueTask<AssetEnsureResult> EnsureAsync(
        string manifestPath,
        string? outputDirectory,
        bool offline,
        bool useHardLink,
        CancellationToken cancellationToken
    ) {
        var manifest = await manifests.ParseAsync(
                manifestPath,
                cancellationToken)
            .ConfigureAwait(false);
        var provider = providers.Get(manifest.Provider);
        var cached = new Dictionary<string, AssetCacheEntry>(
            StringComparer.Ordinal);
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (var asset in manifest.Assets) {
            try {
                var entry = await cache.EnsureAsync(
                        asset,
                        provider,
                        offline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (entry is not null) {
                    cached[asset.Sha256] = entry;
                } else if (asset.Optional) {
                    warnings.Add(
                        $"Optional asset '{asset.Output}' is not cached.");
                } else {
                    errors.Add(
                        $"Asset '{asset.Output}' is missing from the cache.");
                }
            } catch (AssetProviderException error) when (asset.Optional) {
                warnings.Add(error.Message);
            } catch (Exception error) when (
                error is AssetProviderException or
                AssetIntegrityException or
                IOException or
                UnauthorizedAccessException) {
                errors.Add(error.Message);
            }
        }

        if (errors.Count > 0) {
            throw new AssetOperationException(errors);
        }

        await referenceStore.SaveAsync(
                manifestPath,
                manifest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(outputDirectory)) {
            await AssetMaterializer.MaterializeAsync(
                    outputDirectory,
                    manifest,
                    cached,
                    useHardLink,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new AssetEnsureResult(
            provider.License,
            [.. cached.Values.OrderBy(entry => entry.Sha256, StringComparer.Ordinal)],
            warnings);
    }

    public async ValueTask<AssetVerificationReport> VerifyAsync(
        string manifestPath,
        CancellationToken cancellationToken
    ) {
        var manifest = await manifests.ParseAsync(
                manifestPath,
                cancellationToken)
            .ConfigureAwait(false);
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (var asset in manifest.Assets) {
            var path = cache.GetAssetPath(asset.Sha256);
            if (!File.Exists(path)) {
                if (asset.Optional) {
                    warnings.Add(
                        $"Optional asset '{asset.Output}' is not cached.");
                } else {
                    errors.Add(
                        $"Asset '{asset.Output}' is missing from the cache.");
                }

                continue;
            }

            if (!await cache.IsValidAsync(
                    path,
                    asset.Sha256,
                    cancellationToken)
                .ConfigureAwait(false)) {
                errors.Add(
                    $"Asset '{asset.Output}' failed SHA-256 validation.");
            }
        }

        return new AssetVerificationReport(errors, warnings);
    }

    public async ValueTask<AssetPrunePlan> PruneAsync(
        bool dryRun,
        bool yes,
        CancellationToken cancellationToken
    ) {
        var cached = await cache.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var references = await referenceStore.LoadAllAsync(cancellationToken)
            .ConfigureAwait(false);
        var referenced = references
            .SelectMany(reference => reference.Assets)
            .Select(asset => asset.Sha256)
            .ToHashSet(StringComparer.Ordinal);
        var plan = cached
            .Where(entry => !referenced.Contains(entry.Sha256))
            .OrderBy(entry => entry.Sha256, StringComparer.Ordinal)
            .ToArray();
        var result = new AssetPrunePlan(
            plan,
            plan.Sum(entry => entry.Length));
        if (dryRun || !yes) {
            return result;
        }

        foreach (var entry in plan) {
            cancellationToken.ThrowIfCancellationRequested();
            cache.Delete(entry);
        }

        return result;
    }

    public async ValueTask<(
        IReadOnlyList<AssetCacheEntry> Cached,
        IReadOnlyList<AssetManifestReference> References
    )> ListAsync(CancellationToken cancellationToken) {
        var cached = await cache.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var references = await referenceStore.LoadAllAsync(cancellationToken)
            .ConfigureAwait(false);
        return (cached, references);
    }

    public ValueTask<bool> ForgetAsync(
        string manifestPath,
        CancellationToken cancellationToken
    ) => referenceStore.ForgetAsync(
        manifestPath,
        cancellationToken);
}
