using System.Text.Json.Serialization;
using Ampheck.RockchipToolkit.Cli.Assets.Model;
using Tomlyn.Serialization;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

[TomlSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[TomlSerializable(typeof(AssetManifestDocument))]
internal partial class AssetTomlContext : TomlSerializerContext;
