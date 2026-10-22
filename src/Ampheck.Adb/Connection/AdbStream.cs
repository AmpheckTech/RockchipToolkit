using System.Threading.Channels;
using Ampheck.Adb.Protocol;

namespace Ampheck.Adb.Connection;

public sealed class AdbStream : Stream {
    private readonly uint _maximumPayload;
    private readonly uint _localId;
    private readonly Func<
        uint,
        uint,
        AdbCommand,
        ReadOnlyMemory<byte>,
        CancellationToken,
        ValueTask> _send;
    private readonly Action<uint> _remove;
    private readonly Channel<AdbMessage> _incoming =
        Channel.CreateUnbounded<AdbMessage>(
            new UnboundedChannelOptions {
                SingleReader = true,
                SingleWriter = true
            });
    private readonly Queue<AdbMessage> _pending = new();
    private uint _remoteId;
    private byte[]? _readBuffer;
    private int _readOffset;
    private bool _opened;
    private bool _closed;
    private bool _disposed;

    internal AdbStream(
        uint maximumPayload,
        uint localId,
        Func<
            uint,
            uint,
            AdbCommand,
            ReadOnlyMemory<byte>,
            CancellationToken,
            ValueTask> send,
        Action<uint> remove) {
        _maximumPayload = maximumPayload;
        _localId = localId;
        _send = send;
        _remove = remove;
    }

    public override bool CanRead => !_disposed && !_closed;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed && !_closed;
    public override long Length => throw new NotSupportedException();

    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return ReadAsync(buffer.AsMemory(offset, count))
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        return await ReadAsync(
                buffer,
                readTimeout: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        TimeSpan? readTimeout,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty) {
            return 0;
        }

        if (_readBuffer is not null) {
            return CopyAvailable(buffer);
        }

        while (true) {
            var message = _pending.Count > 0
                ? _pending.Dequeue()
                : await ReadIncomingAsync(
                        readTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            switch (message.Command) {
                case AdbCommand.Okay when message.Arg1 == _localId:
                    continue;
                case AdbCommand.Write:
                    await SendCommandAsync(
                            AdbCommand.Okay,
                            message.Arg0,
                            ReadOnlyMemory<byte>.Empty,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (message.Data.Length == 0) {
                        continue;
                    }

                    _readBuffer = message.Data;
                    _readOffset = 0;
                    return CopyAvailable(buffer);
                case AdbCommand.Close:
                    await SendCommandAsync(
                            AdbCommand.Close,
                            message.Arg0,
                            ReadOnlyMemory<byte>.Empty,
                            cancellationToken)
                        .ConfigureAwait(false);
                    _closed = true;
                    _remove(_localId);
                    _incoming.Writer.TryComplete();
                    return 0;
                default:
                    throw new IOException(
                        $"Unexpected ADB stream command " +
                        $"{message.Command}.");
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count) {
        WriteAsync(buffer.AsMemory(offset, count))
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_opened || _closed) {
            throw new IOException("The ADB stream is closed.");
        }

        var offset = 0;
        while (offset < buffer.Length) {
            var length = (int)Math.Min(
                _maximumPayload,
                checked((uint)(buffer.Length - offset)));
            await SendCommandAsync(
                    AdbCommand.Write,
                    _remoteId,
                    buffer.Slice(offset, length),
                    cancellationToken)
                .ConfigureAwait(false);
            offset += length;
        }
    }

    public override void Flush() {
    }

    public override Task FlushAsync(
        CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public override async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        if (!_closed) {
            try {
                await SendCommandAsync(
                        AdbCommand.Close,
                        _remoteId,
                        ReadOnlyMemory<byte>.Empty,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            } catch (Exception error) when (
                error is IOException or ObjectDisposedException) {
            }
        }

        _closed = true;
        _incoming.Writer.TryComplete();
        _remove(_localId);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    internal async ValueTask<AdbMessage> ReadOpeningMessageAsync(
        CancellationToken cancellationToken) {
        try {
            return await _incoming.Reader.ReadAsync(cancellationToken)
                .ConfigureAwait(false);
        } catch (ChannelClosedException error) {
            throw new IOException(
                "The ADB stream closed while opening.",
                error.InnerException);
        }
    }

    internal void Open(uint remoteId) {
        _remoteId = remoteId;
        _opened = true;
    }

    internal async ValueTask AcceptOpeningMessageAsync(
        AdbMessage message,
        CancellationToken cancellationToken) {
        if (message.Command == AdbCommand.Write) {
            _remoteId = message.Arg0;
            await SendCommandAsync(
                    AdbCommand.Okay,
                    message.Arg0,
                    ReadOnlyMemory<byte>.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            if (message.Data.Length > 0) {
                _pending.Enqueue(message);
            }

            return;
        }

        throw new IOException(
            $"Unexpected ADB stream command {message.Command} " +
            "while opening.");
    }

    internal void Receive(AdbMessage message) {
        if (!_incoming.Writer.TryWrite(message)) {
            _closed = true;
        }
    }

    internal ValueTask AbortAsync(
        Exception? error = null) {
        _closed = true;
        _incoming.Writer.TryComplete(error);
        _remove(_localId);
        return ValueTask.CompletedTask;
    }

    private int CopyAvailable(Memory<byte> destination) {
        var remaining = _readBuffer!.Length - _readOffset;
        var count = Math.Min(remaining, destination.Length);
        _readBuffer.AsMemory(_readOffset, count)
            .CopyTo(destination);
        _readOffset += count;
        if (_readOffset >= _readBuffer.Length) {
            _readBuffer = null;
            _readOffset = 0;
        }

        return count;
    }

    private async ValueTask SendCommandAsync(
        AdbCommand command,
        uint remoteId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) {
        await _send(
                _localId,
                remoteId,
                command,
                data,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<AdbMessage> ReadIncomingAsync(
        TimeSpan? readTimeout,
        CancellationToken cancellationToken) {
        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        if (readTimeout is { } duration &&
            duration != Timeout.InfiniteTimeSpan) {
            timeout.CancelAfter(duration);
        }

        try {
            return await _incoming.Reader.ReadAsync(timeout.Token)
                .ConfigureAwait(false);
        } catch (ChannelClosedException error) {
            if (_closed) {
                return new AdbMessage(
                    AdbCommand.Close,
                    0,
                    _localId,
                    []);
            }

            throw new IOException(
                "The ADB stream ended unexpectedly.",
                error.InnerException);
        }
    }
}
