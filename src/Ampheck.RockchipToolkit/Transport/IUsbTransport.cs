namespace Ampheck.RockchipToolkit.Transport;

/// <summary>Bulk and control operations required by RockUSB.</summary>
public interface IUsbTransport : IAsyncDisposable {
    UsbDeviceInfo Device { get; }

    ValueTask WriteBulkAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    ValueTask<int> ReadBulkAsync(
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    ValueTask<int> ControlTransferAsync(
        UsbControlRequest request,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);

    ValueTask ResetAsync(CancellationToken cancellationToken = default);
}
