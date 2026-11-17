namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetManifest(
    int Schema,
    string Provider,
    IReadOnlyList<AssetEntry> Assets
);
