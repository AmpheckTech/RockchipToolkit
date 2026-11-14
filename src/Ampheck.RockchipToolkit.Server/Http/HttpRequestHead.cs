using System.Globalization;
using System.Text;

namespace Ampheck.RockchipToolkit.Server.Http;

internal sealed record HttpRequestHead(
    string Method,
    string Target,
    string Version,
    IReadOnlyDictionary<string, string> Headers) {
    private const int MaximumHeaderBytes = 16 * 1024;

    public string Path {
        get {
            var separator = Target.IndexOf('?');
            return Uri.UnescapeDataString(
                separator < 0
                    ? Target
                    : Target[..separator]);
        }
    }

    public string? Query(string name) {
        var separator = Target.IndexOf('?');
        if (separator < 0 || separator == Target.Length - 1) {
            return null;
        }

        foreach (var field in Target[(separator + 1)..].Split('&')) {
            var equals = field.IndexOf('=');
            var key = equals < 0 ? field : field[..equals];
            if (!Uri.UnescapeDataString(key).Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            return equals < 0
                ? string.Empty
                : Uri.UnescapeDataString(field[(equals + 1)..]);
        }

        return null;
    }

    public string? Header(string name) =>
        Headers.TryGetValue(name, out var value)
            ? value
            : null;

    public long? ContentLength {
        get {
            var value = Header("Content-Length");
            if (value is null) {
                return null;
            }

            return long.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var length) &&
                length >= 0
                    ? length
                    : throw new InvalidDataException(
                        "The HTTP Content-Length header is invalid.");
        }
    }

    public bool IsWebSocketUpgrade =>
        Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            Header("Upgrade"),
            "websocket",
            StringComparison.OrdinalIgnoreCase) &&
        Header("Connection")?.Contains(
            "Upgrade",
            StringComparison.OrdinalIgnoreCase) == true;

    public static async ValueTask<HttpRequestHead> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        var bytes = new byte[MaximumHeaderBytes];
        var length = 0;
        var matched = 0;
        while (length < bytes.Length && matched < 4) {
            var read = await stream.ReadAsync(
                    bytes.AsMemory(length, 1),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException(
                    "The HTTP request ended before its headers.");
            }

            length++;
            matched = (matched, bytes[length - 1]) switch {
                (0, (byte)'\r') => 1,
                (1, (byte)'\n') => 2,
                (2, (byte)'\r') => 3,
                (3, (byte)'\n') => 4,
                (_, (byte)'\r') => 1,
                _ => 0
            };
        }

        if (matched != 4) {
            throw new InvalidDataException(
                "The HTTP request headers are too large.");
        }

        var lines = Encoding.ASCII.GetString(bytes, 0, length)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var requestLine = lines.Length == 0
            ? []
            : lines[0].Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);
        if (requestLine is not [var method, var target, var version] ||
            !version.StartsWith(
                "HTTP/1.",
                StringComparison.Ordinal)) {
            throw new InvalidDataException(
                "The HTTP request line is invalid.");
        }

        var headers = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1)) {
            var separator = line.IndexOf(':');
            if (separator <= 0) {
                throw new InvalidDataException(
                    "The HTTP request contains an invalid header.");
            }

            headers[line[..separator].Trim()] =
                line[(separator + 1)..].Trim();
        }

        return new HttpRequestHead(
            method,
            target,
            version,
            headers);
    }
}
