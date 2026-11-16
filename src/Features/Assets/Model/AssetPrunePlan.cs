namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetPrunePlan(
    IReadOnlyList<AssetCacheEntry> Entries,
    long TotalBytes
);
