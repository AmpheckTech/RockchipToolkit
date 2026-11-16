using System.Net;
using Ampheck.RockchipToolkit.Features.Assets.Model;

namespace Ampheck.RockchipToolkit.Features.Assets.Providers;

internal sealed class RockchipRkbinProvider : IAssetProvider {
    private const string CommitPattern =
        "^(?:[0-9a-f]{40}|[0-9a-f]{64})$";
    private const string RawRepository =
        "https://raw.githubusercontent.com/rockchip-linux/rkbin";

    public string Id => "rockchip-rkbin";

    public AssetLicense License { get; } = new(
        "Rockchip Software License",
        new Uri(
            "https://raw.githubusercontent.com/rockchip-linux/rkbin/" +
            "3e288fe814e059dd06833495f845cab04ac20a5c/LICENSE"));

    public void Validate(AssetEntry entry) {
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                entry.Commit,
                CommitPattern)) {
            throw new AssetManifestException([
                $"Asset '{entry.Output}': commit must be a complete " +
                "lowercase Git object id."
            ]);
        }
    }

    public Uri Resolve(AssetEntry entry) {
        var path = string.Join(
            '/',
            entry.Path.Split('/').Select(Uri.EscapeDataString));
        return new Uri(
            $"{RawRepository}/{entry.Commit}/{path}");
    }

    public AssetProviderException MapException(
        AssetEntry entry,
        Exception exception
    ) {
        var detail = exception is HttpRequestException {
            StatusCode: HttpStatusCode.NotFound
        }
                ? "asset was not found upstream"
                : exception.Message;
        return new AssetProviderException(
            $"Provider '{Id}' failed for '{entry.Path}': {detail}",
            exception);
    }
}
