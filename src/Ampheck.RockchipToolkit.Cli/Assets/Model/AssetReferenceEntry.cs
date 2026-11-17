namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetReferenceEntry(
    string Sha256,
    string Output,
    string Purpose
);
