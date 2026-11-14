using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Server.Rpc;

internal static class JsonWebSocketProtocol {
    private const int MaximumMessageBytes = 1024 * 1024;

    public static async ValueTask SendAsync<T>(
        WebSocket socket,
        T value,
        CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(
            value,
            typeof(T),
            ToolkitJsonSerializerContext.Default);
        await socket.SendAsync(
                Encoding.UTF8.GetBytes(json),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<JsonRpcRequest?> ReceiveAsync(
        WebSocket socket,
        CancellationToken cancellationToken) {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        using var message = new MemoryStream();
        try {
            while (true) {
                var result = await socket.ReceiveAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Text) {
                    throw new InvalidDataException(
                        "The JSON-RPC WebSocket accepts text messages only.");
                }

                message.Write(buffer, 0, result.Count);
                if (message.Length > MaximumMessageBytes) {
                    throw new InvalidDataException(
                        "The JSON-RPC message is too large.");
                }

                if (result.EndOfMessage) {
                    var json = Encoding.UTF8.GetString(
                        message.GetBuffer(),
                        0,
                        checked((int)message.Length));
                    return JsonSerializer.Deserialize(
                        json,
                        typeof(JsonRpcRequest),
                        ToolkitJsonSerializerContext.Default)
                        as JsonRpcRequest;
                }
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
