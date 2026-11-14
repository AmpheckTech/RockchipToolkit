using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed partial class ToolkitRpcClient {
    public async ValueTask<BlobReference> UploadBlobAsync(
        string path,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException(
                $"Blob source '{path}' was not found.",
                path);
        }

        var blobId = Guid.NewGuid().ToString("N");
        await using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var content = new ProgressStreamContent(
            source,
            source.Length,
            progress);
        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");
        using var request = CreateBlobRequest(
            HttpMethod.Put,
            blobId);
        request.Content = content;
        request.Content.Headers.ContentLength = source.Length;
        request.Headers.TryAddWithoutValidation(
            "X-ARKT-File-Name",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    Path.GetFileName(path))));
        using var response = await ToolkitHttpClientFactory
            .For(request.RequestUri!)
            .SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken)
            .ConfigureAwait(false);
        return await DeserializeAsync<BlobReference>(
                response,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DownloadBlobAsync(
        BlobReference blob,
        string path,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        using var request = CreateBlobRequest(
            HttpMethod.Get,
            blob.BlobId);
        using var response = await ToolkitHttpClientFactory
            .For(request.RequestUri!)
            .SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken)
            .ConfigureAwait(false);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try {
            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destination = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long total = 0;
            while (true) {
                var read = await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                total = checked(total + read);
                progress?.Report(total);
            }

            await destination.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            if (blob.Sha256 is { } expected &&
                !Convert.ToHexString(hash.GetHashAndReset())
                    .Equals(
                        expected,
                        StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException(
                    "The downloaded blob checksum does not match.");
            }

            File.Move(temporary, path, overwrite: true);
        } catch {
            TryDelete(temporary);
            throw;
        }
    }

    public async ValueTask DeleteBlobAsync(
        string blobId,
        CancellationToken cancellationToken = default) {
        using var request = CreateBlobRequest(
            HttpMethod.Delete,
            blobId);
        using var response = await ToolkitHttpClientFactory
            .For(request.RequestUri!)
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    private HttpRequestMessage CreateBlobRequest(
        HttpMethod method,
        string blobId) {
        var request = new HttpRequestMessage(
            method,
            _endpoint.HttpUri($"/api/blobs/{blobId}"));
        request.Headers.UserAgent.ParseAdd(
            "Ampheck.RockchipToolkit/0.1");
        if (!string.IsNullOrEmpty(_token)) {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _token);
        }

        return request;
    }

    private static async ValueTask<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) {
        var json = await response.Content.ReadAsStringAsync(
                cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize(
                json,
                GetTypeInfo<T>())
            ?? throw new InvalidDataException(
                "The server returned an empty response.");
    }

    private static async ValueTask EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) {
        if (response.IsSuccessStatusCode) {
            return;
        }

        var message = await response.Content.ReadAsStringAsync(
                cancellationToken)
            .ConfigureAwait(false);
        throw new HttpRequestException(
            $"Blob request failed with HTTP " +
            $"{(int)response.StatusCode}: {message}",
            inner: null,
            statusCode: response.StatusCode);
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }

    private sealed class ProgressStreamContent(
        Stream source,
        long length,
        IProgress<long>? progress) : HttpContent {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            SerializeCoreAsync(
                stream,
                CancellationToken.None);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken) =>
            SerializeCoreAsync(stream, cancellationToken);

        private async Task SerializeCoreAsync(
            Stream stream,
            CancellationToken cancellationToken) {
            var buffer = new byte[128 * 1024];
            long total = 0;
            while (true) {
                var read = await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                await stream.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                total = checked(total + read);
                progress?.Report(total);
            }

            if (total != length) {
                throw new InvalidDataException(
                    "The blob source changed during upload.");
            }
        }

        protected override bool TryComputeLength(
            out long result) {
            result = length;
            return true;
        }
    }
}
