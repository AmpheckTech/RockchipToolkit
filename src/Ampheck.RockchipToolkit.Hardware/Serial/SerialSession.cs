using System.Threading.Channels;
using Ampheck.RockchipToolkit.Transport.Serial;

namespace Ampheck.RockchipToolkit.Hardware.Serial;

public sealed class SerialSession : ISerialSession {
    private readonly SerialHub _hub;
    private readonly Channel<ReadOnlyMemory<byte>> _output;
    private int _disposed;

    internal SerialSession(
        SerialHub hub,
        string portName,
        Channel<ReadOnlyMemory<byte>> output
    ) {
        _hub = hub;
        PortName = portName;
        _output = output;
    }

    public string PortName { get; }
    public ChannelReader<ReadOnlyMemory<byte>> Output => _output.Reader;

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        return _hub.WriteAsync(data, cancellationToken);
    }

    public ValueTask WaitForConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    ) =>
        _hub.WaitForConnectionAsync(
            timeout,
            cancellationToken);

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        await _hub.DetachAsync(this).ConfigureAwait(false);
        _output.Writer.TryComplete();
    }

    internal bool TryPublish(ReadOnlyMemory<byte> value) =>
        _output.Writer.TryWrite(value);

    internal void Complete() => _output.Writer.TryComplete();
}
