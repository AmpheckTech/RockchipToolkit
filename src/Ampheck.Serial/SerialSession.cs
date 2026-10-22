using System.Threading.Channels;

namespace Ampheck.Serial;

public sealed class SerialSession : IAsyncDisposable {
    private readonly SerialHub _hub;
    private readonly Channel<SerialEvent> _events;
    private int _disposed;

    internal SerialSession(
        SerialHub hub,
        string portName,
        Channel<SerialEvent> events) {
        _hub = hub;
        PortName = portName;
        _events = events;
    }

    public string PortName { get; }
    public ChannelReader<SerialEvent> Events => _events.Reader;

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        return _hub.WriteAsync(data, cancellationToken);
    }

    public ValueTask WaitForConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        _hub.WaitForConnectionAsync(
            timeout,
            cancellationToken);

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        await _hub.DetachAsync(this).ConfigureAwait(false);
        _events.Writer.TryComplete();
    }

    internal bool TryPublish(SerialEvent value) =>
        _events.Writer.TryWrite(value);

    internal void Complete() => _events.Writer.TryComplete();
}
