namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetManifestReference(
    string ManifestPath,
    string Provider,
    DateTimeOffset RecordedAt,
    IReadOnlyList<AssetReferenceEntry> Assets
);
