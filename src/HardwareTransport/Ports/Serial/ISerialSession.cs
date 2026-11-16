using System.Threading.Channels;

namespace Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

public interface ISerialSession : IAsyncDisposable {
    string PortName { get; }
    ChannelReader<ReadOnlyMemory<byte>> Output { get; }

    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    ValueTask WaitForConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
