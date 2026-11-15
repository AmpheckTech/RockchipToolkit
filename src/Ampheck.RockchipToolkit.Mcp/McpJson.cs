using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol;

namespace Ampheck.RockchipToolkit.Mcp;

internal static class McpJson {
    public static JsonSerializerOptions ToolOptions { get; } =
        CreateToolOptions();

    private static JsonSerializerOptions CreateToolOptions() {
        return new JsonSerializerOptions(
            McpJsonUtilities.DefaultOptions) {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                ToolkitJsonSerializerContext.Default,
                McpJsonSerializerContext.Default,
                McpJsonUtilities.DefaultOptions.TypeInfoResolver)
        };
    }
}
