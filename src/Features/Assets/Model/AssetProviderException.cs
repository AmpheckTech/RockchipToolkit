namespace Ampheck.RockchipToolkit.Features.Assets.Model;

internal sealed class AssetProviderException(
    string message,
    Exception? innerException = null
) : Exception(message, innerException);
