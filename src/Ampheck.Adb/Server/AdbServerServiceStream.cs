using System.Net.Sockets;

namespace Ampheck.Adb.Server;

public sealed class AdbServerServiceStream : Stream {
    private readonly AdbServerConnection _connection;
    private bool _disposed;

    internal AdbServerServiceStream(
        AdbServerConnection connection) {
        _connection = connection;
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => !_disposed;
    public override long Length => throw new NotSupportedException();

    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return _connection.Stream.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _connection.Stream.ReadAsync(
            buffer,
            cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count) {
        _connection.Stream.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _connection.Stream.WriteAsync(
            buffer,
            cancellationToken);
    }

    public override void Flush() {
        _connection.Stream.Flush();
    }

    public override Task FlushAsync(
        CancellationToken cancellationToken) {
        return _connection.Stream.FlushAsync(cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing) {
        if (!_disposed && disposing) {
            _disposed = true;
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _connection.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
