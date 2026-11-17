namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed class AssetProviderException(
    string message,
    Exception? innerException = null
) : Exception(message, innerException);
