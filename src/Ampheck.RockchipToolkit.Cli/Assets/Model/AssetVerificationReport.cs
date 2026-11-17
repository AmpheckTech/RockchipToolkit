namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed record AssetVerificationReport(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
