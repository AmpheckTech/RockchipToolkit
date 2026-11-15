using Ampheck.RockchipToolkit.Rpc;
using Microsoft.Extensions.Configuration;

namespace Ampheck.RockchipToolkit.Mcp;

internal sealed record McpBridgeOptions(
    ToolkitEndpoint Endpoint,
    string ListenUrl,
    string? Token,
    ToolkitFileAccessMode FileAccess) {
    public static McpBridgeOptions FromConfiguration(
        IConfiguration configuration) {
        var server = configuration["server"] ??
            Environment.GetEnvironmentVariable("ARKT_SERVER") ??
            ToolkitServerDefaults.Endpoint;
        var token = configuration["token"] ??
            Environment.GetEnvironmentVariable("ARKT_SERVER_TOKEN");
        var listenUrl = configuration["listen"] ??
            Environment.GetEnvironmentVariable("ARKT_MCP_LISTEN") ??
            "http://127.0.0.1:5071";
        if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https")) {
            throw new ArgumentException(
                $"Invalid MCP listen URL '{listenUrl}'.");
        }

        var fileAccess = ToolkitFileAccess.Parse(
            configuration["file-mode"] ??
            Environment.GetEnvironmentVariable("ARKT_FILE_MODE"));
        return new McpBridgeOptions(
            ToolkitEndpoint.Parse(server),
            listenUrl,
            string.IsNullOrWhiteSpace(token) ? null : token,
            fileAccess);
    }
}
