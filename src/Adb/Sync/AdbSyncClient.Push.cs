namespace Ampheck.RockchipToolkit.Adb.Sync;

public static partial class AdbSyncClient {
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
        CancellationToken cancellationToken = default
    ) {
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
}
