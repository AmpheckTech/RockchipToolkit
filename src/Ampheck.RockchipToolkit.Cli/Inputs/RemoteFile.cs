namespace Ampheck.RockchipToolkit.Cli.Inputs;

internal sealed class RemoteFile(
    HttpResponseMessage response,
    Stream content,
    long? length
) : IAsyncDisposable {
    public Stream Content { get; } = content;
    public long? Length { get; } = length;

    public static async ValueTask<RemoteFile> OpenAsync(
        Uri uri,
        CancellationToken cancellationToken
    ) {
        var response = await RemoteHttpClientFactory.For(uri)
            .GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            var statusCode = response.StatusCode;
            response.Dispose();
            throw new HttpRequestException(
                $"Remote file request failed with HTTP " +
                $"{(int)statusCode}.",
                inner: null,
                statusCode: statusCode);
        }

        var content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return new RemoteFile(
            response,
            content,
            response.Content.Headers.ContentLength);
    }

    public async ValueTask DisposeAsync() {
        await Content.DisposeAsync().ConfigureAwait(false);
        response.Dispose();
    }
}
