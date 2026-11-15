using System.ComponentModel;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol.Server;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

[McpServerToolType]
public sealed class ServerTools(ToolkitRpcClient client) {
    [McpServerTool(
        Name = "get_server_info",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("Get toolkit server identity and available RPC methods.")]
    public async Task<ServerInfoResult> GetServerInfo(
        CancellationToken cancellationToken = default) {
        return await client.GetServerInfoAsync(
            cancellationToken);
    }
}
