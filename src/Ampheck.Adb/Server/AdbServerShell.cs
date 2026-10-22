using System.Text;
using Ampheck.Adb.Connection;

namespace Ampheck.Adb.Server;

public sealed class AdbServerShell : IAdbShellStream {
    private readonly AdbServerServiceStream _stream;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private bool _disposed;

    internal AdbServerShell(
        AdbServerServiceStream stream) {
        _stream = stream;
    }

    public async Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default,
        TimeSpan? readTimeout = null) {
        ArgumentNullException.ThrowIfNull(output);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[16 * 1024];
        var characters = new char[16 * 1024];
        while (true) {
            var read = await _stream.ReadAsync(
                    bytes,
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                break;
            }

            var count = decoder.GetChars(
                bytes,
                0,
                read,
                characters,
                0);
            if (count > 0) {
                output(new string(characters, 0, count));
            }
        }

        var finalCount = decoder.GetChars(
            ReadOnlySpan<byte>.Empty,
            characters,
            flush: true);
        if (finalCount > 0) {
            output(new string(characters, 0, finalCount));
        }
    }

    public ValueTask WriteAsync(
        string text,
        CancellationToken cancellationToken = default) {
        return WriteAsync(
            Encoding.UTF8.GetBytes(text),
            cancellationToken);
    }

    public ValueTask ResizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default) {
        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _sendGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await _stream.WriteAsync(data, cancellationToken)
                .ConfigureAwait(false);
        } finally {
            _sendGate.Release();
        }
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _sendGate.Dispose();
        return _stream.DisposeAsync();
    }
}
