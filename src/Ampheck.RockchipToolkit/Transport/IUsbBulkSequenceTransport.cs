namespace Ampheck.RockchipToolkit.Transport;

/// <summary>Writes multiple USB bulk buffers on one transport session.</summary>
public interface IUsbBulkSequenceTransport {
    ValueTask WriteBulkSequenceAsync(
        ReadOnlyMemory<byte> first,
        ReadOnlyMemory<byte> second,
        CancellationToken cancellationToken = default);
}
