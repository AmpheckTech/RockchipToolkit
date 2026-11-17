using System.Text.Json.Serialization;
using Ampheck.RockchipToolkit.Cli.Assets.Model;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AssetManifestReference))]
internal partial class AssetJsonContext : JsonSerializerContext;
