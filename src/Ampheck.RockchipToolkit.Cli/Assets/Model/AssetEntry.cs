namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetEntry(
    string Commit,
    string Path,
    string Output,
    string Sha256,
    string Purpose,
    bool Optional
);
