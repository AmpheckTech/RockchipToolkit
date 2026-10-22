using System.Buffers;
using System.Net.WebSockets;

namespace Ampheck.RockchipToolkit.Rpc;

internal static class ToolkitWebSocketProtocol {
    public static async ValueTask SendAsync(WebSocket socket, ToolkitFrame frame, CancellationToken cancellationToken) {
        var data = ToolkitWireCodec.Encode(frame);
        await socket.SendAsync(data, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask<ToolkitFrame?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken) {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        using var message = new MemoryStream();
        try {
            while (true) {
                var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Binary) {
                    throw new InvalidDataException("The toolkit WebSocket accepts binary messages only.");
                }

                message.Write(buffer, 0, result.Count);
                if (message.Length > ToolkitWireCodec.HeaderLength + ToolkitWireCodec.MaximumPayloadLength) {
                    throw new InvalidDataException("The toolkit WebSocket message is too large.");
                }

                if (result.EndOfMessage) {
                    return ToolkitWireCodec.Decode(message.GetBuffer().AsSpan(0, checked((int)message.Length)));
                }
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
