namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetManifest(
    int Schema,
    string Provider,
    IReadOnlyList<AssetEntry> Assets
);
