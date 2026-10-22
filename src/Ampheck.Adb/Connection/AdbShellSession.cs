using System.Text;

namespace Ampheck.Adb.Connection;

/// <summary>One opened ADB shell stream, either command or interactive mode.</summary>
public sealed class AdbShellSession : IAdbShellStream {
    private readonly AdbStream _stream;
    private bool _disposed;

    internal AdbShellSession(AdbStream stream) {
        _stream = stream;
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stream.WriteAsync(data, cancellationToken);
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
                    readTimeout,
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

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return _stream.DisposeAsync();
    }
}
