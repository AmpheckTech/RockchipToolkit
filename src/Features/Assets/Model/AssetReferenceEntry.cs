namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetReferenceEntry(
    string Sha256,
    string Output,
    string Purpose
);
