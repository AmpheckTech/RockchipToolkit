using Ampheck.Adb.Device;

namespace Ampheck.Adb.Sync;

internal sealed class AdbSyncTransfer : IAsyncDisposable {
    private readonly IAsyncDisposable? _connection;
    private readonly IAsyncDisposable? _provider;
    private bool _disposed;

    public AdbSyncTransfer(
        Stream stream,
        IAsyncDisposable? connection = null,
        IAsyncDisposable? provider = null) {
        Stream = stream;
        _connection = connection;
        _provider = provider;
    }

    public Stream Stream { get; }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await Stream.DisposeAsync().ConfigureAwait(false);
        if (_connection is not null) {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        if (_provider is not null) {
            await _provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
