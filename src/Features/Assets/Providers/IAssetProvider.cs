using Ampheck.RockchipToolkit.Features.Assets.Model;

namespace Ampheck.RockchipToolkit.Features.Assets.Providers;

internal interface IAssetProvider {
    string Id { get; }
    AssetLicense License { get; }
    void Validate(AssetEntry entry);
    Uri Resolve(AssetEntry entry);
    AssetProviderException MapException(
        AssetEntry entry,
        Exception exception
    );
}
