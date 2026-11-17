using System.Text.Json.Serialization;

namespace Ampheck.RockchipToolkit.Cli.Assets.Model;

internal sealed class AssetDocument {
    public string Commit { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public bool Optional { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Unknown { get; set; }
}
