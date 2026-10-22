using System.Net.Http.Headers;
using Ampheck.RockchipToolkit.Rpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.IO;

public sealed class ToolkitOutput {
    private readonly string? _path;
    private readonly Uri? _uri;

    private ToolkitOutput(string? path, Uri? uri) {
        _path = path;
        _uri = uri;
    }

    public static ToolkitOutput Create(string location) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        return WebUri.TryGetHttp(location, out var uri)
            ? new ToolkitOutput(null, uri)
            : new ToolkitOutput(location, null);
    }

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        ILogger logger,
        CancellationToken cancellationToken = default,
        HttpClient? httpClient = null) {
        if (_path is not null) {
            await File.WriteAllBytesAsync(
                    _path,
                    data,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var uri = _uri!;
        logger.LogInformation(
            "Uploading {ByteCount} bytes to {RemoteHost} using HTTP PUT",
            data.Length,
            WebUri.GetDisplayAuthority(uri));
        using var content = new ReadOnlyMemoryContent(data);
        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");
        using var response = await (httpClient ?? ToolkitHttpClientFactory.For(uri))
            .PutAsync(uri, content, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            throw new HttpRequestException(
                $"HTTP upload failed with status " +
                $"{(int)response.StatusCode}.");
        }
    }
}
