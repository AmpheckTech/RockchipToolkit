namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetManifestReference(
    string ManifestPath,
    string Provider,
    DateTimeOffset RecordedAt,
    AssetReferenceEntry[] Assets
);
