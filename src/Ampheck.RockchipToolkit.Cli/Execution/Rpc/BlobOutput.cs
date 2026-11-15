using System.Net.Http.Headers;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class BlobOutput {
    public static async Task WriteAsync(
        ToolkitRpcClient client,
        FileReference file,
        string destination,
        ConsoleRenderer console,
        ILogger logger,
        CancellationToken cancellationToken = default) {
        if (!Uri.TryCreate(
                destination,
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme is not ("http" or "https")) {
            if (file.Path is not null) {
                return;
            }

            var blob = ToBlob(file);
            using var progress = CreateProgress(
                console,
                blob.FileName,
                blob.Length);
            await client.DownloadBlobAsync(
                    blob,
                    destination,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var temporary = Path.Combine(
            Path.GetTempPath(),
            $"arkt-output-{Guid.NewGuid():N}.bin");
        try {
            var blob = ToBlob(file);
            using (var progress = CreateProgress(
                console,
                blob.FileName,
                blob.Length)) {
                await client.DownloadBlobAsync(
                        blob,
                        temporary,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            logger.LogInformation(
                "Uploading {ByteCount} bytes to {RemoteHost}",
                blob.Length,
                uri.Authority);
            await using var source = new FileStream(
                temporary,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            using var content = new StreamContent(source);
            content.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/octet-stream");
            content.Headers.ContentLength = blob.Length;
            using var response = await ToolkitHttpClientFactory
                .For(uri)
                .PutAsync(
                    uri,
                    content,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                throw new HttpRequestException(
                    $"HTTP upload failed with status " +
                    $"{(int)response.StatusCode}.");
            }
        } finally {
            TryDelete(temporary);
        }
    }

    public static async Task CopyToStandardOutputAsync(
        ToolkitRpcClient client,
        FileReference file,
        CancellationToken cancellationToken = default) {
        if (file.Path is not null) {
            await using var local = new FileStream(
                file.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            await local.CopyToAsync(
                    Console.OpenStandardOutput(),
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var blob = ToBlob(file);
        var temporary = Path.Combine(
            Path.GetTempPath(),
            $"arkt-history-{Guid.NewGuid():N}.bin");
        try {
            await client.DownloadBlobAsync(
                    blob,
                    temporary,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await using var source = new FileStream(
                temporary,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            await source.CopyToAsync(
                    Console.OpenStandardOutput(),
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            TryDelete(temporary);
        }
    }

    private static ProgressBar? CreateProgress(
        ConsoleRenderer console,
        string label,
        long total) =>
        console.IsInteractive && total > 0
            ? new ProgressBar(console, label, total)
            : null;

    private static BlobReference ToBlob(
        FileReference file) {
        return new BlobReference {
            BlobId = file.BlobId ??
                throw new InvalidDataException(
                    "The server returned no blob or local file."),
            FileName = file.FileName,
            Length = file.Length,
            Sha256 = file.Sha256
        };
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
