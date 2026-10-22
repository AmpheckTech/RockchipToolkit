using Ampheck.Adb.Connection;

namespace Ampheck.Adb.Server;

internal sealed class AdbServerDeviceLease : IAsyncDisposable {
    private bool _disposed;

    public AdbServerDeviceLease(AdbStream stream) {
        Stream = stream;
    }

    public AdbStream Stream { get; }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await Stream.DisposeAsync().ConfigureAwait(false);
    }
}
