using System.Globalization;
using System.Text;
using System.Text.Json;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Server.Http;

internal static class HttpResponses {
    public static async ValueTask WriteJsonAsync<T>(
        Stream stream,
        int statusCode,
        string reason,
        T value,
        CancellationToken cancellationToken) {
        var json = JsonSerializer.Serialize(
            value,
            typeof(T),
            ToolkitJsonSerializerContext.Default);
        var body = Encoding.UTF8.GetBytes(json);
        await WriteHeadAsync(
                stream,
                statusCode,
                reason,
                "application/json; charset=utf-8",
                body.LongLength,
                cancellationToken)
            .ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask WriteErrorAsync(
        Stream stream,
        int statusCode,
        string reason,
        string message,
        CancellationToken cancellationToken) {
        await WriteJsonAsync(
                stream,
                statusCode,
                reason,
                new TextResult { Text = message },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask WriteHeadAsync(
        Stream stream,
        int statusCode,
        string reason,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken) {
        var header = string.Format(
            CultureInfo.InvariantCulture,
            "HTTP/1.1 {0} {1}\r\n" +
            "Content-Type: {2}\r\n" +
            "Content-Length: {3}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, PUT, DELETE, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: Authorization, Content-Type, " +
            "X-ARKT-File-Name, X-ARKT-Client\r\n" +
            "Access-Control-Expose-Headers: Content-Length\r\n" +
            "Access-Control-Max-Age: 600\r\n" +
            "Connection: close\r\n\r\n",
            statusCode,
            reason,
            contentType,
            contentLength);
        await stream.WriteAsync(
                Encoding.ASCII.GetBytes(header),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask WriteContinueAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        await stream.WriteAsync(
                "HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
