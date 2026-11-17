using System.Text.Json.Serialization;

namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed class AssetManifestDocument {
    public long Schema { get; set; }
    public string Provider { get; set; } = string.Empty;
    public List<AssetDocument> Assets { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, object>? Unknown { get; set; }
}
