namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed class AssetOperationException(
    IReadOnlyList<string> errors
) : Exception(string.Join(Environment.NewLine, errors)) {
    public IReadOnlyList<string> Errors { get; } = errors;
}
