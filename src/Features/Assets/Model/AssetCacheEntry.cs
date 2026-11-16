namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetCacheEntry(
    string Sha256,
    string Path,
    long Length
);
