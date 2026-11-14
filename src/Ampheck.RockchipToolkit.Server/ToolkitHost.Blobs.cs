using System.Text;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Http;

namespace Ampheck.RockchipToolkit.Server;

public sealed partial class ToolkitHost {
    private async Task HandleBlobAsync(
        Stream stream,
        HttpRequestHead request,
        CancellationToken serverToken) {
        var blobId = request.Path["/api/blobs/".Length..];
        try {
            switch (request.Method) {
                case "PUT":
                    await UploadBlobAsync(
                            stream,
                            request,
                            blobId,
                            serverToken)
                        .ConfigureAwait(false);
                    return;
                case "GET":
                    await DownloadBlobAsync(
                            stream,
                            blobId,
                            serverToken)
                        .ConfigureAwait(false);
                    return;
                case "DELETE":
                    await WriteDeleteResponseAsync(
                            stream,
                            _blobs.Delete(blobId),
                            serverToken)
                        .ConfigureAwait(false);
                    return;
                default:
                    await HttpResponses.WriteErrorAsync(
                            stream,
                            405,
                            "Method Not Allowed",
                            "The blob endpoint supports GET, PUT, and DELETE.",
                            serverToken)
                        .ConfigureAwait(false);
                    return;
            }
        } catch (ToolkitRpcException error) {
            await HttpResponses.WriteErrorAsync(
                    stream,
                    error.Code == JsonRpcErrorCodes.NotFound
                        ? 404
                        : 409,
                    error.Code == JsonRpcErrorCodes.NotFound
                        ? "Not Found"
                        : "Conflict",
                    error.Message,
                    serverToken)
                .ConfigureAwait(false);
        } catch (InvalidDataException error) {
            await HttpResponses.WriteErrorAsync(
                    stream,
                    400,
                    "Bad Request",
                    error.Message,
                    serverToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask UploadBlobAsync(
        Stream stream,
        HttpRequestHead request,
        string blobId,
        CancellationToken cancellationToken) {
        var length = request.ContentLength ??
            throw new InvalidDataException(
                "The blob upload requires Content-Length.");
        if (length > _options.TransferLimits.MaximumBlobBytes) {
            await HttpResponses.WriteErrorAsync(
                    stream,
                    413,
                    "Content Too Large",
                    "The blob exceeds the configured size limit.",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (request.Header("Expect")?.Equals(
                "100-continue",
                StringComparison.OrdinalIgnoreCase) == true) {
            await HttpResponses.WriteContinueAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var fileName = DecodeFileName(
            request.Header("X-ARKT-File-Name"));
        var reference = await _blobs.AcceptAsync(
                blobId,
                fileName,
                length,
                stream,
                cancellationToken)
            .ConfigureAwait(false);
        await HttpResponses.WriteJsonAsync(
                stream,
                201,
                "Created",
                reference,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask DownloadBlobAsync(
        Stream stream,
        string blobId,
        CancellationToken cancellationToken) {
        var entry = _blobs.Get(blobId);
        await HttpResponses.WriteHeadAsync(
                stream,
                200,
                "OK",
                "application/octet-stream",
                entry.Reference.Length,
                cancellationToken)
            .ConfigureAwait(false);
        await using var source = new FileStream(
            entry.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(stream, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask WriteDeleteResponseAsync(
        Stream stream,
        bool deleted,
        CancellationToken cancellationToken) {
        await HttpResponses.WriteJsonAsync(
                stream,
                deleted ? 200 : 404,
                deleted ? "OK" : "Not Found",
                new TextResult {
                    Text = deleted ? "Deleted" : "Blob not found."
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string DecodeFileName(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "blob.bin";
        }

        try {
            return Encoding.UTF8.GetString(
                Convert.FromBase64String(value));
        } catch (FormatException) {
            throw new InvalidDataException(
                "The blob file name header is invalid.");
        }
    }
}
