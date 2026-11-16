namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed record AssetVerificationReport(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
