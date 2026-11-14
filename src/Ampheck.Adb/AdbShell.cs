using Ampheck.Adb.Connection;

namespace Ampheck.Adb;

public sealed class AdbShell : IAdbShellStream {
    private readonly IAdbShellStream _shell;
    private bool _disposed;

    internal AdbShell(IAdbShellStream shell) {
        _shell = shell;
    }

    public ValueTask WriteAsync(
        string text,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.WriteAsync(text, cancellationToken);
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.WriteAsync(data, cancellationToken);
    }

    public ValueTask ResizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.ResizeAsync(
            columns,
            rows,
            cancellationToken);
    }

    public Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.ReadAsync(
            output,
            cancellationToken);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await _shell.DisposeAsync().ConfigureAwait(false);
    }
}
