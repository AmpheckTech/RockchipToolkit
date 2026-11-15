using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server.IO;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed class InputFile : IAsyncDisposable {
    private const int CopyBufferSize = 128 * 1024;
    private readonly bool _temporary;
    private readonly ILogger _logger;

    private InputFile(
        string path,
        bool temporary,
        ILogger logger) {
        Path = path;
        _temporary = temporary;
        _logger = logger;
    }

    public string Path { get; }

    public static async ValueTask<InputFile> OpenAsync(
        string location,
        FirmwareFileContext context,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (WebUri.TryGetHttp(location, out var uri)) {
            return await DownloadAsync(
                    uri,
                    context,
                    cancellationToken,
                    ToolkitHttpClientFactory.For(uri))
                .ConfigureAwait(false);
        }

        if (!File.Exists(location)) {
            throw new FileNotFoundException(
                $"Input file '{location}' was not found.",
                location);
        }

        context.Logger.LogDebug(
            "Using local input file {FilePath} ({ByteCount} bytes)",
            location,
            new FileInfo(location).Length);
        return new InputFile(
            location,
            temporary: false,
            context.Logger);
    }

    public ValueTask DisposeAsync() {
        if (!_temporary) {
            return ValueTask.CompletedTask;
        }

        try {
            File.Delete(Path);
            _logger.LogDebug(
                "Deleted temporary download {FilePath}",
                Path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
            _logger.LogWarning(
                error,
                "Unable to delete temporary download {FilePath}",
                Path);
        }

        return ValueTask.CompletedTask;
    }

    private static async ValueTask<InputFile> DownloadAsync(
        Uri uri,
        FirmwareFileContext context,
        CancellationToken cancellationToken,
        HttpClient httpClient) {
        context.Logger.LogInformation(
            "Downloading remote input from {RemoteHost}",
            WebUri.GetDisplayAuthority(uri));
        var fileName = System.IO.Path.GetFileName(uri.AbsolutePath);
        await context.Report(
                string.IsNullOrEmpty(fileName)
                    ? "Downloading firmware from HTTP"
                    : $"Downloading {fileName} from HTTP")
            .ConfigureAwait(false);

        using var response = await httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            throw new HttpRequestException(
                $"Remote file request failed with HTTP " +
                $"{(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        var contentLength =
            response.Content.Headers.ContentLength;
        if (contentLength is { } declaredLength &&
            declaredLength >
            context.Limits.MaximumBlobBytes) {
            throw new InvalidDataException(
                $"Remote input exceeds the configured " +
                $"{context.Limits.MaximumBlobBytes} byte limit.");
        }

        var temporaryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"ampheck-download-{Guid.NewGuid():N}.tmp");
        try {
            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[CopyBufferSize];
            long totalRead = 0;
            while (true) {
                var read = await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                if (read >
                    context.Limits.MaximumBlobBytes - totalRead) {
                    throw new InvalidDataException(
                        "Remote input exceeds the configured size limit.");
                }

                await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                totalRead += read;
                if (context.Progress is not null &&
                    contentLength is > 0) {
                    await context.Progress(
                            totalRead,
                            contentLength.Value)
                        .ConfigureAwait(false);
                }
            }

            await destination.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            context.Logger.LogInformation(
                "Downloaded {ByteCount} bytes to {TemporaryPath}",
                destination.Length,
                temporaryPath);
            if (!string.IsNullOrEmpty(fileName)) {
                await context.Report(
                        $"Download complete: {fileName}")
                    .ConfigureAwait(false);
            }

            return new InputFile(
                temporaryPath,
                temporary: true,
                context.Logger);
        } catch {
            TryDelete(temporaryPath, context.Logger);
            throw;
        }
    }

    private static void TryDelete(
        string path,
        ILogger logger) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
            logger.LogWarning(
                error,
                "Unable to delete incomplete download {FilePath}",
                path);
        }
    }
}
