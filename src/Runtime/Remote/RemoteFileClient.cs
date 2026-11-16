using Ampheck.RockchipToolkit.Core.Ports.Files;

namespace Ampheck.RockchipToolkit.Runtime.Remote;

internal sealed class RemoteFileClient : IRemoteFileClient {
    public async ValueTask<IRemoteFile> OpenAsync(
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
}
