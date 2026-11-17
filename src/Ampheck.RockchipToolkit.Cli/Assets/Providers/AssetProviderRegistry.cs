using Ampheck.RockchipToolkit.Cli.Assets.Model;

namespace Ampheck.RockchipToolkit.Cli.Assets.Providers;

internal sealed class AssetProviderRegistry(
    IEnumerable<IAssetProvider> providers
) {
    private readonly IReadOnlyDictionary<string, IAssetProvider> _providers =
        providers.ToDictionary(
            provider => provider.Id,
            StringComparer.Ordinal);

    public IAssetProvider Get(string id) =>
        _providers.TryGetValue(id, out var provider)
            ? provider
            : throw new AssetManifestException([
                $"Unknown asset provider '{id}'."
            ]);
}
