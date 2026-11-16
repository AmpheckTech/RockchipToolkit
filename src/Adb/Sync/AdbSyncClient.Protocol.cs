using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Adb.Sync;

public static partial class AdbSyncClient {
    private static async Task WritePathAsync(
        Stream sync,
        string path,
        CancellationToken cancellationToken
    ) {
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
        CancellationToken cancellationToken
    ) {
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
        CancellationToken cancellationToken
    ) {
        var data = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        await sync.WriteAsync(data, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadIdAsync(
        Stream sync,
        CancellationToken cancellationToken
    ) {
        var id = new byte[4];
        await sync.ReadExactlyAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return id;
    }

    private static async Task<int> ReadInt32Async(
        Stream sync,
        CancellationToken cancellationToken
    ) {
        var data = new byte[4];
        await sync.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32LittleEndian(data);
    }

    private static async Task ReadCompletionAsync(
        Stream sync,
        CancellationToken cancellationToken
    ) {
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
        CancellationToken cancellationToken
    ) {
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
}
