using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

internal static class ToolkitWebSocketHandshake {
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaximumHeaderLength = 16 * 1024;
    private const string ProtocolName = "arkt.bin";

    public static async Task<WebSocket> AcceptAsync(Stream stream, CancellationToken cancellationToken) {
        var headerBytes = new byte[MaximumHeaderLength];
        var length = 0;
        var matched = 0;
        while (length < headerBytes.Length && matched < 4) {
            var read = await stream.ReadAsync(headerBytes.AsMemory(length, 1), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException("The WebSocket handshake ended unexpectedly.");
            }

            length++;
            matched = (matched, headerBytes[length - 1]) switch {
                (0, (byte)'\r') => 1,
                (1, (byte)'\n') => 2,
                (2, (byte)'\r') => 3,
                (3, (byte)'\n') => 4,
                (_, (byte)'\r') => 1,
                _ => 0
            };
        }

        if (matched != 4) {
            throw new InvalidDataException("The WebSocket handshake header is too large.");
        }

        var lines = Encoding.ASCII.GetString(headerBytes, 0, length)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var requestLine = lines.Length == 0
            ? []
            : lines[0].Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);
        if (requestLine is not ["GET", var path, "HTTP/1.1"] ||
            !path.Equals("/arkt", StringComparison.Ordinal)) {
            throw new InvalidDataException("The WebSocket request line is invalid.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1)) {
            var separator = line.IndexOf(':');
            if (separator > 0) {
                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        if (!headers.TryGetValue("Sec-WebSocket-Key", out var key) ||
            !headers.TryGetValue("Upgrade", out var upgrade) ||
            !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase) ||
            !headers.TryGetValue("Connection", out var connection) ||
            !connection.Contains("Upgrade", StringComparison.OrdinalIgnoreCase) ||
            !headers.TryGetValue("Sec-WebSocket-Version", out var version) ||
            version != "13" ||
            !headers.TryGetValue("Sec-WebSocket-Protocol", out var protocols) ||
            !protocols.Split(',').Any(value =>
                value.Trim().Equals(
                    ProtocolName,
                    StringComparison.Ordinal)) ||
            !IsValidKey(key)) {
            throw new InvalidDataException("The WebSocket upgrade request is invalid.");
        }

        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n" +
            $"Sec-WebSocket-Protocol: {ProtocolName}\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        return WebSocket.CreateFromStream(stream, isServer: true, ProtocolName, TimeSpan.FromSeconds(30));
    }

    private static bool IsValidKey(string value) {
        try {
            return Convert.FromBase64String(value).Length == 16;
        } catch (FormatException) {
            return false;
        }
    }
}
