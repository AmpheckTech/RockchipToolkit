namespace Ampheck.RockchipToolkit.Images;

internal sealed class RockchipImageSubStream : Stream {
    private readonly Stream _parent;
    private readonly object _sync;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public RockchipImageSubStream(
        Stream parent,
        object sync,
        long start,
        long length) {
        _parent = parent;
        _sync = sync;
        _start = start;
        _length = length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position {
        get => _position;
        set => _position = Math.Clamp(value, 0, _length);
    }

    public override int Read(Span<byte> buffer) {
        var available = (int)Math.Min(buffer.Length, _length - _position);
        if (available <= 0) {
            return 0;
        }

        lock (_sync) {
            _parent.Position = _start + _position;
            var read = _parent.Read(buffer[..available]);
            _position += read;
            return read;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return Read(buffer.AsSpan(offset, count));
    }

    public override long Seek(long offset, SeekOrigin origin) {
        Position = origin switch {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        return _position;
    }

    public override void Flush() {
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }
}
