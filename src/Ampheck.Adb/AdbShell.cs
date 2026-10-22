using Ampheck.Adb.Connection;

namespace Ampheck.Adb;

public sealed class AdbShell : IAdbShellStream {
    private readonly IAdbShellStream _shell;
    private readonly IAsyncDisposable? _connection;
    private readonly IAsyncDisposable? _provider;
    private bool _disposed;

    internal AdbShell(
        IAdbShellStream shell,
        IAsyncDisposable? connection = null,
        IAsyncDisposable? provider = null) {
        _shell = shell;
        _connection = connection;
        _provider = provider;
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
        CancellationToken cancellationToken = default,
        TimeSpan? readTimeout = null) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.ReadAsync(
            output,
            cancellationToken,
            readTimeout);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await _shell.DisposeAsync().ConfigureAwait(false);
        if (_connection is not null) {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        if (_provider is not null) {
            await _provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
