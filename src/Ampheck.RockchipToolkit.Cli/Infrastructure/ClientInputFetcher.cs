using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal sealed class ClientInputFetcher(
    ILogger logger,
    ConsoleRenderer console) {
    private const int BufferSize = 128 * 1024;

    public async Task<string> DownloadAsync(
        string location,
        CancellationToken cancellationToken) {
        if (!Uri.TryCreate(
                location,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not ("http" or "https")) {
            throw new ArgumentException(
                $"Client fetch requires an HTTP(S) URL: '{location}'.",
                nameof(location));
        }

        if (uri.AbsolutePath.EndsWith(
                "/",
                StringComparison.Ordinal)) {
            throw new NotSupportedException(
                "Client fetch does not support a firmware directory URL. " +
                "Use a .zip URL or let the server fetch the directory.");
        }

        var fileName = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName)) {
            fileName = "input.bin";
        }

        var extension = Path.GetExtension(fileName);
        var destination = Path.Combine(
            Path.GetTempPath(),
            $"ampheck-client-{Guid.NewGuid():N}{extension}");
        logger.LogInformation(
            "Fetching {RemoteHost}/{FileName} on the client",
            uri.Authority,
            fileName);
        console.WriteStatus(
            $"Downloading {fileName} from HTTP using the client");

        using var response = await ToolkitHttpClientFactory.For(uri)
            .GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            throw new HttpRequestException(
                $"Client download failed with HTTP " +
                $"{(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        try {
            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var target = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            var total = response.Content.Headers.ContentLength;
            using var progress =
                console.IsInteractive &&
                total is > 0
                    ? new ProgressBar(
                        console,
                        fileName,
                        total.Value)
                    : null;
            var buffer = new byte[BufferSize];
            long written = 0;
            while (true) {
                var read = await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                await target.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                written += read;
                progress?.Report(written);
            }

            await target.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Client downloaded {ByteCount} bytes to {Path}",
                written,
                destination);
            console.WriteStatus(
                $"Download complete: {fileName}",
                complete: true);
            return destination;
        } catch {
            TryDelete(destination);
            throw;
        }
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
