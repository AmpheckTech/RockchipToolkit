namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetEnsureResult(
    AssetLicense License,
    IReadOnlyList<AssetCacheEntry> Assets,
    IReadOnlyList<string> Warnings
);
