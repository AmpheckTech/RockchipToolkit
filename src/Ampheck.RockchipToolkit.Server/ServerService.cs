using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Generated;

namespace Ampheck.RockchipToolkit.Server;

[JsonRpcService(JsonRpcServiceLifetime.Singleton)]
internal sealed class ServerService(ToolkitHostOptions options) {

    [JsonRpcMethod(JsonRpcMethods.ServerInfo)]
    public ValueTask<ServerInfoResult> InfoAsync(
        EmptyParams parameters,
        JsonRpcCallContext context) {
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ServerInfoResult {
            InstanceId = options.InstanceId,
            HostName = options.HostName,
            Version = ToolkitVersion.Current,
            ProtocolMajor = ToolkitProtocolVersion.Major,
            ProtocolMinor = ToolkitProtocolVersion.Minor,
            Methods = [
                .. GeneratedJsonRpcCatalog.Methods,
                JsonRpcMethods.TaskList,
                JsonRpcMethods.TaskGet,
                JsonRpcMethods.TaskCancel,
                JsonRpcMethods.TaskSubscribe
            ]
        });
    }
}
