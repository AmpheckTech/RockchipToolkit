using System.Buffers.Binary;
using System.Text;

namespace Ampheck.Adb.Sync;

public static class AdbSyncClient {
    private const int TransferBufferSize = 64 * 1024;
    private static readonly byte[] Stat = "STAT"u8.ToArray();
    private static readonly byte[] List = "LIST"u8.ToArray();
    private static readonly byte[] Send = "SEND"u8.ToArray();
    private static readonly byte[] Receive = "RECV"u8.ToArray();
    private static readonly byte[] Data = "DATA"u8.ToArray();
    private static readonly byte[] Done = "DONE"u8.ToArray();
    private static readonly byte[] Okay = "OKAY"u8.ToArray();
    private static readonly byte[] Fail = "FAIL"u8.ToArray();
    private static readonly byte[] Dent = "DENT"u8.ToArray();

    public static async Task PushAsync(
        Stream sync,
        string localPath,
        string remotePath,
        UnixFileMode mode =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.GroupRead |
            UnixFileMode.OtherRead,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        await using var source = new FileStream(
            localPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            TransferBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await WriteIdAsync(sync, Send, cancellationToken)
            .ConfigureAwait(false);
        var modeText = Convert.ToString(
            checked((int)mode),
            8);
        await WritePathAsync(
                sync,
                $"{remotePath},{modeText}",
                cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[TransferBufferSize];
        long total = 0;
        while (true) {
            var read = await source.ReadAsync(
                    buffer,
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                break;
            }

            await WriteIdAsync(sync, Data, cancellationToken)
                .ConfigureAwait(false);
            await WriteInt32Async(
                    sync,
                    read,
                    cancellationToken)
                .ConfigureAwait(false);
            await sync.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken)
                .ConfigureAwait(false);
            total += read;
            progress?.Report(total);
        }

        await WriteIdAsync(sync, Done, cancellationToken)
            .ConfigureAwait(false);
        var modifiedSeconds = checked((int)(
            new DateTimeOffset(File.GetLastWriteTimeUtc(localPath))
                .ToUnixTimeSeconds()));
        await WriteInt32Async(
                sync,
                modifiedSeconds,
                cancellationToken)
            .ConfigureAwait(false);
        await ReadCompletionAsync(sync, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task PullAsync(
        Stream sync,
        string remotePath,
        string localPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        await using var destination = new FileStream(
            localPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            TransferBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await WriteIdAsync(sync, Receive, cancellationToken)
            .ConfigureAwait(false);
        await WritePathAsync(sync, remotePath, cancellationToken)
            .ConfigureAwait(false);

        long total = 0;
        while (true) {
            var id = await ReadIdAsync(sync, cancellationToken)
                .ConfigureAwait(false);
            if (id.SequenceEqual(Done)) {
                break;
            }

            if (id.SequenceEqual(Fail)) {
                await ThrowFailureAsync(sync, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!id.SequenceEqual(Data)) {
                throw new InvalidDataException(
                    "The ADB sync stream returned an unexpected command.");
            }

            var length = await ReadInt32Async(sync, cancellationToken)
                .ConfigureAwait(false);
            if (length < 0) {
                throw new InvalidDataException(
                    "The ADB sync payload length is invalid.");
            }

            var buffer = new byte[length];
            await sync.ReadExactlyAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            await destination.WriteAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            total += length;
            progress?.Report(total);
        }

        await destination.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<AdbFileInfo> StatFileAsync(
        Stream sync,
        string remotePath,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);

        await WriteIdAsync(sync, Stat, cancellationToken)
            .ConfigureAwait(false);
        await WritePathAsync(sync, remotePath, cancellationToken)
            .ConfigureAwait(false);
        var id = await ReadIdAsync(sync, cancellationToken)
            .ConfigureAwait(false);
        if (id.SequenceEqual(Fail)) {
            await ThrowFailureAsync(sync, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!id.SequenceEqual(Stat)) {
            throw new InvalidDataException(
                "The ADB sync stream returned an invalid STAT response.");
        }

        var mode = await ReadUInt32Async(sync, cancellationToken)
            .ConfigureAwait(false);
        var size = await ReadUInt32Async(sync, cancellationToken)
            .ConfigureAwait(false);
        var modifiedSeconds = await ReadUInt32Async(
                sync,
                cancellationToken)
            .ConfigureAwait(false);
        return new AdbFileInfo(
            GetName(remotePath),
            mode,
            size,
            DateTimeOffset.FromUnixTimeSeconds(modifiedSeconds));
    }

    public static async Task<IReadOnlyList<AdbFileInfo>>
        ListDirectoryAsync(
        Stream sync,
        string remoteDirectory,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteDirectory);

        await WriteIdAsync(sync, List, cancellationToken)
            .ConfigureAwait(false);
        await WritePathAsync(
                sync,
                remoteDirectory,
                cancellationToken)
            .ConfigureAwait(false);
        var entries = new List<AdbFileInfo>();
        while (true) {
            var id = await ReadIdAsync(sync, cancellationToken)
                .ConfigureAwait(false);
            if (id.SequenceEqual(Done)) {
                break;
            }

            if (id.SequenceEqual(Fail)) {
                await ThrowFailureAsync(sync, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!id.SequenceEqual(Dent)) {
                throw new InvalidDataException(
                    "The ADB sync stream returned an invalid LIST response.");
            }

            var mode = await ReadUInt32Async(sync, cancellationToken)
                .ConfigureAwait(false);
            var size = await ReadUInt32Async(sync, cancellationToken)
                .ConfigureAwait(false);
            var modifiedSeconds = await ReadUInt32Async(
                    sync,
                    cancellationToken)
                .ConfigureAwait(false);
            var nameLength = await ReadInt32Async(
                    sync,
                    cancellationToken)
                .ConfigureAwait(false);
            if (nameLength is < 0 or > 4096) {
                throw new InvalidDataException(
                    "The ADB sync directory entry length is invalid.");
            }

            var nameBytes = new byte[nameLength];
            await sync.ReadExactlyAsync(
                    nameBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            var name = Encoding.UTF8.GetString(nameBytes);
            if (name is "." or "..") {
                continue;
            }

            entries.Add(new AdbFileInfo(
                name,
                mode,
                size,
                DateTimeOffset.FromUnixTimeSeconds(
                    modifiedSeconds)));
        }

        return entries.AsReadOnly();
    }

    public static Task QuitAsync(
        Stream sync,
        CancellationToken cancellationToken = default) {
        return WriteIdAsync(
            sync,
            "QUIT"u8.ToArray(),
            cancellationToken);
    }

    private static async Task WritePathAsync(
        Stream sync,
        string path,
        CancellationToken cancellationToken) {
        var bytes = Encoding.UTF8.GetBytes(path);
        await WriteInt32Async(
                sync,
                bytes.Length,
                cancellationToken)
            .ConfigureAwait(false);
        await sync.WriteAsync(bytes, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteIdAsync(
        Stream sync,
        ReadOnlyMemory<byte> id,
        CancellationToken cancellationToken) {
        if (id.Length != 4) {
            throw new ArgumentException(
                "ADB sync identifiers must contain four bytes.",
                nameof(id));
        }

        await sync.WriteAsync(id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteInt32Async(
        Stream sync,
        int value,
        CancellationToken cancellationToken) {
        var data = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        await sync.WriteAsync(data, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadIdAsync(
        Stream sync,
        CancellationToken cancellationToken) {
        var id = new byte[4];
        await sync.ReadExactlyAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return id;
    }

    private static async Task<int> ReadInt32Async(
        Stream sync,
        CancellationToken cancellationToken) {
        var data = new byte[4];
        await sync.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(data);
    }

    private static async Task<uint> ReadUInt32Async(
        Stream sync,
        CancellationToken cancellationToken) {
        var data = new byte[4];
        await sync.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    private static async Task ReadCompletionAsync(
        Stream sync,
        CancellationToken cancellationToken) {
        var id = await ReadIdAsync(sync, cancellationToken)
            .ConfigureAwait(false);
        if (id.SequenceEqual(Okay)) {
            return;
        }

        if (id.SequenceEqual(Fail)) {
            await ThrowFailureAsync(sync, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidDataException(
            "The ADB sync stream returned an invalid completion status.");
    }

    private static async Task ThrowFailureAsync(
        Stream sync,
        CancellationToken cancellationToken) {
        var length = await ReadInt32Async(sync, cancellationToken)
            .ConfigureAwait(false);
        if (length is < 0 or > 4096) {
            throw new IOException(
                "The ADB sync operation failed.");
        }

        var message = new byte[length];
        await sync.ReadExactlyAsync(message, cancellationToken)
            .ConfigureAwait(false);
        throw new IOException(
            string.IsNullOrWhiteSpace(
                Encoding.UTF8.GetString(message))
                ? "The ADB sync operation failed."
                : Encoding.UTF8.GetString(message));
    }

    private static string GetName(string path) {
        var trimmed = path.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');
        return separator < 0
            ? trimmed
            : trimmed[(separator + 1)..];
    }
}
