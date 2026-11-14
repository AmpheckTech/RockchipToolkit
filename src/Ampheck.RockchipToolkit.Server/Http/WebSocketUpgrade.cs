using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Ampheck.RockchipToolkit.Server.Http;

internal static class WebSocketUpgrade {
    private const string WebSocketGuid =
        "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    public static async ValueTask<WebSocket> AcceptAsync(
        Stream stream,
        HttpRequestHead request,
        string protocol,
        CancellationToken cancellationToken) {
        var key = request.Header("Sec-WebSocket-Key");
        var version = request.Header("Sec-WebSocket-Version");
        var protocols = request.Header("Sec-WebSocket-Protocol");
        if (!request.IsWebSocketUpgrade ||
            string.IsNullOrWhiteSpace(key) ||
            !IsValidKey(key) ||
            version != "13" ||
            protocols?.Split(',').Any(value =>
                value.Trim().Equals(
                    protocol,
                    StringComparison.Ordinal)) != true) {
            throw new InvalidDataException(
                "The WebSocket upgrade request is invalid.");
        }

        var accept = Convert.ToBase64String(
            SHA1.HashData(
                Encoding.ASCII.GetBytes(key + WebSocketGuid)));
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n" +
            $"Sec-WebSocket-Protocol: {protocol}\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken)
            .ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
        return WebSocket.CreateFromStream(
            stream,
            isServer: true,
            protocol,
            TimeSpan.FromSeconds(30));
    }

    private static bool IsValidKey(string value) {
        try {
            return Convert.FromBase64String(value).Length == 16;
        } catch (FormatException) {
            return false;
        }
    }
}
