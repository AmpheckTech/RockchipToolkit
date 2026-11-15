using System.Text.Json.Serialization;
using Ampheck.RockchipToolkit.Mcp.Tools;

namespace Ampheck.RockchipToolkit.Mcp;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StorageWriteToolResult))]
internal sealed partial class McpJsonSerializerContext :
    JsonSerializerContext;
