namespace Ampheck.RockchipToolkit.Adb.Sync;

public static partial class AdbSyncClient {
    public static async Task PullAsync(
        Stream sync,
        string remotePath,
        string localPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default
    ) {
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
}
