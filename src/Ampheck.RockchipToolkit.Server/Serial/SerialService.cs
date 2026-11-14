using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;

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
    public async ValueTask<BlobReference> HistoryAsync(
        SerialHistoryParams parameters,
        JsonRpcCallContext context) {
        var history = serial.GetHistory(parameters);
        return await blobs.CreateAsync(
                $"{Path.GetFileName(parameters.PortName)}-history.bin",
                (destination, cancellationToken) =>
                    destination.WriteAsync(
                            history,
                            cancellationToken)
                        .AsTask(),
                context.CancellationToken)
            .ConfigureAwait(false);
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
