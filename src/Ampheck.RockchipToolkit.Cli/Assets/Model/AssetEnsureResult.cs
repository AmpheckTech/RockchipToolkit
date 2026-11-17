namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetEnsureResult(
    AssetLicense License,
    IReadOnlyList<AssetCacheEntry> Assets,
    IReadOnlyList<string> Warnings
);
