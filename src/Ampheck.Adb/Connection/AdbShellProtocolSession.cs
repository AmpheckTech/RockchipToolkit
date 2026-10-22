using System.Buffers.Binary;
using System.Text;

namespace Ampheck.Adb.Connection;

internal sealed class AdbShellProtocolSession : IAdbShellStream {
    private const byte StdinId = 0;
    private const byte StdoutId = 1;
    private const byte StderrId = 2;
    private const byte ExitId = 3;
    private const byte CloseStdinId = 4;
    private const byte WindowSizeId = 5;
    private const int HeaderLength = 5;
    private const int MaximumPayloadLength = 16 * 1024 * 1024;

    private readonly Stream _stream;
    private readonly TimeSpan? _readTimeout;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Decoder _stdoutDecoder = Encoding.UTF8.GetDecoder();
    private readonly Decoder _stderrDecoder = Encoding.UTF8.GetDecoder();
    private bool _closed;
    private bool _disposed;

    public AdbShellProtocolSession(
        Stream stream,
        TimeSpan? readTimeout = null) {
        _stream = stream;
        _readTimeout = readTimeout;
    }

    public ValueTask WriteAsync(
        string text,
        CancellationToken cancellationToken = default) {
        return WriteAsync(
            Encoding.UTF8.GetBytes(text),
            cancellationToken);
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_closed) {
            throw new IOException(
                "The ADB shell has already exited.");
        }

        return WritePacketsAsync(
            StdinId,
            data,
            cancellationToken);
    }

    public ValueTask ResizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (columns <= 0 || rows <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                "Terminal dimensions must be positive.");
        }

        var size = Encoding.ASCII.GetBytes(
            $"{rows}x{columns},0x0\0");
        return WritePacketsAsync(
            WindowSizeId,
            size,
            cancellationToken);
    }

    public async Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default,
        TimeSpan? readTimeout = null) {
        ArgumentNullException.ThrowIfNull(output);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timeout = readTimeout ?? _readTimeout;
        var header = new byte[HeaderLength];
        while (await TryReadExactlyAsync(
                header,
                timeout,
                cancellationToken)
            .ConfigureAwait(false)) {
            var id = header[0];
            var length = BinaryPrimitives.ReadUInt32LittleEndian(
                header.AsSpan(1));
            if (length > MaximumPayloadLength) {
                throw new InvalidDataException(
                    $"ADB shell packet is too large ({length} bytes).");
            }

            var payload = new byte[length];
            await ReadExactlyAsync(
                    payload,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            switch (id) {
                case StdoutId:
                    WriteDecoded(
                        payload,
                        _stdoutDecoder,
                        output);
                    break;
                case StderrId:
                    WriteDecoded(
                        payload,
                        _stderrDecoder,
                        output);
                    break;
                case ExitId:
                    _closed = true;
                    break;
                case CloseStdinId:
                    break;
                default:
                    throw new InvalidDataException(
                        $"Unknown ADB shell packet id {id}.");
            }

            if (_closed) {
                break;
            }
        }

        WriteDecoderRemainder(_stdoutDecoder, output);
        WriteDecoderRemainder(_stderrDecoder, output);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _writeGate.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask WritePacketsAsync(
        byte id,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) {
        await _writeGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            var offset = 0;
            while (offset < data.Length) {
                var length = Math.Min(
                    data.Length - offset,
                    MaximumPayloadLength);
                var header = new byte[HeaderLength];
                header[0] = id;
                BinaryPrimitives.WriteUInt32LittleEndian(
                    header.AsSpan(1),
                    checked((uint)length));
                await _stream.WriteAsync(
                        header,
                        cancellationToken)
                    .ConfigureAwait(false);
                await _stream.WriteAsync(
                        data.Slice(offset, length),
                        cancellationToken)
                    .ConfigureAwait(false);
                offset += length;
            }
        } finally {
            _writeGate.Release();
        }
    }

    private async ValueTask<bool> TryReadExactlyAsync(
        Memory<byte> buffer,
        TimeSpan? timeout,
        CancellationToken cancellationToken) {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await ReadStreamAsync(
                    buffer[offset..],
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                if (offset == 0) {
                    return false;
                }

                throw new EndOfStreamException(
                    "The ADB shell packet ended unexpectedly.");
            }

            offset += read;
        }

        return true;
    }

    private async ValueTask ReadExactlyAsync(
        Memory<byte> buffer,
        TimeSpan? timeout,
        CancellationToken cancellationToken) {
        if (!await TryReadExactlyAsync(
                buffer,
                timeout,
                cancellationToken)
            .ConfigureAwait(false)) {
            throw new EndOfStreamException(
                "The ADB shell packet ended unexpectedly.");
        }
    }

    private ValueTask<int> ReadStreamAsync(
        Memory<byte> buffer,
        TimeSpan? timeout,
        CancellationToken cancellationToken) {
        return _stream is AdbStream adbStream
            ? adbStream.ReadAsync(
                buffer,
                timeout,
                cancellationToken)
            : _stream.ReadAsync(
                buffer,
                cancellationToken);
    }

    private static void WriteDecoded(
        ReadOnlySpan<byte> data,
        Decoder decoder,
        Action<string> output) {
        var characters = new char[
            Encoding.UTF8.GetMaxCharCount(data.Length)];
        var count = decoder.GetChars(
            data,
            characters,
            flush: false);
        if (count > 0) {
            output(new string(characters, 0, count));
        }
    }

    private static void WriteDecoderRemainder(
        Decoder decoder,
        Action<string> output) {
        var characters = new char[4];
        var count = decoder.GetChars(
            ReadOnlySpan<byte>.Empty,
            characters,
            flush: true);
        if (count > 0) {
            output(new string(characters, 0, count));
        }
    }
}
