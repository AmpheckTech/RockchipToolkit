using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;
using Ampheck.RockchipToolkit.Server.Rpc;

namespace Ampheck.RockchipToolkit.Server.Serial;

[JsonRpcService(JsonRpcServiceLifetime.Singleton)]
internal sealed class SerialService(
    ToolkitSerialService serial,
    SerialTerminalRegistry terminals,
    BlobStore blobs) {

    [JsonRpcMethod(JsonRpcMethods.SerialList)]
    public ValueTask<SerialListResult> ListAsync(
        EmptyParams parameters,
        JsonRpcCallContext context) {
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new SerialListResult {
            Ports = serial.ListDevices()
                .Select(device => device.PortName)
                .ToArray()
        });
    }

    [JsonRpcMethod(JsonRpcMethods.SerialOpen)]
    public async ValueTask<EmptyResult> OpenAsync(
        SerialOpenParams parameters,
        JsonRpcCallContext context) {
        await serial.OpenPortAsync(parameters)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    [JsonRpcMethod(JsonRpcMethods.SerialClose)]
    public async ValueTask<SerialCloseResult> CloseAsync(
        SerialCloseParams parameters,
        JsonRpcCallContext context) {
        context.CancellationToken.ThrowIfCancellationRequested();
        return new SerialCloseResult {
            Closed = await serial.CloseAsync(parameters.PortName)
                .ConfigureAwait(false)
        };
    }

    [JsonRpcMethod(JsonRpcMethods.SerialClear)]
    public ValueTask<SerialClearResult> ClearAsync(
        SerialPortParams parameters,
        JsonRpcCallContext context) {
        context.CancellationToken.ThrowIfCancellationRequested();
        serial.ClearHistory(parameters);
        return ValueTask.FromResult(new SerialClearResult {
            Cleared = true
        });
    }

    [JsonRpcMethod(JsonRpcMethods.SerialHistory)]
    public async ValueTask<FileReference> HistoryAsync(
        SerialHistoryParams parameters,
        JsonRpcCallContext context) {
        var history = serial.GetHistory(parameters);
        var fileName =
            $"{Path.GetFileName(parameters.PortName)}-history.bin";
        if (parameters.OutputServerPath is not null) {
            var path = RpcFileAccess.RequireServerPath(
                context,
                parameters.OutputServerPath,
                "Serial history output");
            if (Directory.Exists(path)) {
                path = Path.Combine(path, fileName);
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(
                    path,
                    history,
                    context.CancellationToken)
                .ConfigureAwait(false);
            return RpcFileAccess.CreateFileReference(path);
        }

        var blob = await blobs.CreateAsync(
                fileName,
                (destination, cancellationToken) =>
                    destination.WriteAsync(
                            history,
                            cancellationToken)
                        .AsTask(),
                context.CancellationToken)
            .ConfigureAwait(false);
        return RpcFileAccess.CreateFileReference(blob);
    }

    [JsonRpcMethod(JsonRpcMethods.SerialTerminal)]
    public ValueTask<StreamSessionResult> TerminalAsync(
        SerialTerminalParams parameters,
        JsonRpcCallContext context) {
        context.CancellationToken.ThrowIfCancellationRequested();
        serial.EnsureOpen(parameters.PortName);
        return ValueTask.FromResult(terminals.Register(parameters));
    }
}
