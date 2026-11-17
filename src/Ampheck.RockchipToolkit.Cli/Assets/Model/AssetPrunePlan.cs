namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetPrunePlan(
    IReadOnlyList<AssetCacheEntry> Entries,
    long TotalBytes
);
