using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Server.Blobs;

internal sealed class BlobStore : IAsyncDisposable {
    private const int BufferSize = 128 * 1024;
    private readonly string _root;
    private readonly long _maximumBytes;
    private readonly ConcurrentDictionary<string, BlobEntry> _blobs = [];
    private readonly ConcurrentDictionary<string, byte> _reserved = [];
    private int _disposed;

    public BlobStore(long maximumBytes) {
        if (maximumBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _maximumBytes = maximumBytes;
        _root = Path.Combine(
            Path.GetTempPath(),
            $"ampheck-blobs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public async ValueTask<BlobReference> AcceptAsync(
        string blobId,
        string fileName,
        long length,
        Stream source,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        ValidateId(blobId);
        if (length < 0 || length > _maximumBytes) {
            throw new InvalidDataException(
                $"Blob length must be between 0 and {_maximumBytes} bytes.");
        }

        if (!_reserved.TryAdd(blobId, 0)) {
            throw new ToolkitRpcException(
                JsonRpcErrorCodes.Conflict,
                $"Blob '{blobId}' is already being uploaded or exists.");
        }

        var safeName = NormalizeFileName(fileName);
        var directory = Path.Combine(_root, blobId);
        var path = Path.Combine(directory, safeName);
        try {
            if (_blobs.ContainsKey(blobId)) {
                throw new ToolkitRpcException(
                    JsonRpcErrorCodes.Conflict,
                    $"Blob '{blobId}' already exists.");
            }

            Directory.CreateDirectory(directory);
            await using var destination = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
            var buffer = new byte[BufferSize];
            long total = 0;
            while (total < length) {
                var wanted = (int)Math.Min(
                    buffer.Length,
                    length - total);
                var read = await source.ReadAsync(
                        buffer.AsMemory(0, wanted),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    throw new InvalidDataException(
                        "The uploaded blob ended before its declared length.");
                }

                total = checked(total + read);

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await destination.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            var reference = new BlobReference {
                BlobId = blobId,
                FileName = safeName,
                Length = total,
                Sha256 = Convert.ToHexString(
                    hash.GetHashAndReset())
            };
            _blobs[blobId] = new BlobEntry(reference, path);
            return reference;
        } catch {
            TryDeleteDirectory(directory);
            throw;
        } finally {
            _reserved.TryRemove(blobId, out _);
        }
    }

    public async ValueTask<BlobReference> CreateAsync(
        string fileName,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken) {
        var blobId = Guid.NewGuid().ToString("N");
        if (!_reserved.TryAdd(blobId, 0)) {
            throw new InvalidOperationException(
                "Unable to reserve a blob identifier.");
        }

        var safeName = NormalizeFileName(fileName);
        var directory = Path.Combine(_root, blobId);
        var path = Path.Combine(directory, safeName);
        try {
            Directory.CreateDirectory(directory);
            await using var destination = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await writeAsync(destination, cancellationToken)
                .ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            if (destination.Length > _maximumBytes) {
                throw new InvalidDataException(
                    $"Blob length exceeds {_maximumBytes} bytes.");
            }

            destination.Position = 0;
            var sha256 = Convert.ToHexString(
                await SHA256.HashDataAsync(
                        destination,
                        cancellationToken)
                    .ConfigureAwait(false));
            var reference = new BlobReference {
                BlobId = blobId,
                FileName = safeName,
                Length = destination.Length,
                Sha256 = sha256
            };
            _blobs[blobId] = new BlobEntry(reference, path);
            return reference;
        } catch {
            TryDeleteDirectory(directory);
            throw;
        } finally {
            _reserved.TryRemove(blobId, out _);
        }
    }

    public BlobEntry Get(string blobId) {
        ValidateId(blobId);
        return _blobs.TryGetValue(blobId, out var entry)
            ? entry
            : throw new ToolkitRpcException(
                JsonRpcErrorCodes.NotFound,
                $"Blob '{blobId}' was not found.");
    }

    public bool Delete(string blobId) {
        if (!_blobs.TryRemove(blobId, out var entry)) {
            return false;
        }

        TryDeleteDirectory(Path.GetDirectoryName(entry.Path)!);
        return true;
    }

    public ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return ValueTask.CompletedTask;
        }

        _blobs.Clear();
        _reserved.Clear();
        TryDeleteDirectory(_root);
        return ValueTask.CompletedTask;
    }

    private static void ValidateId(string blobId) {
        if (!Guid.TryParseExact(blobId, "N", out _)) {
            throw new InvalidDataException(
                "The blob identifier must be a 32-digit hexadecimal value.");
        }
    }

    private static string NormalizeFileName(string fileName) {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name) ||
            name is "." or ".." ||
            name.Length > 255 ||
            name.Any(char.IsControl)) {
            throw new InvalidDataException(
                "The blob file name is invalid.");
        }

        return name;
    }

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}

internal sealed record BlobEntry(
    BlobReference Reference,
    string Path);
