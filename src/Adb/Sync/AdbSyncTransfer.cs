namespace Ampheck.RockchipToolkit.Adb.Sync;

internal sealed class AdbSyncTransfer : IAsyncDisposable {
    private bool _disposed;

    public AdbSyncTransfer(Stream stream) {
        Stream = stream;
    }

    public Stream Stream { get; }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await Stream.DisposeAsync().ConfigureAwait(false);
    }
}
