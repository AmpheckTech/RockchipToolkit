namespace Ampheck.RockchipToolkit.Transport;

/// <summary>Transport-independent device discovery.</summary>
public interface IUsbDeviceProvider : IAsyncDisposable {
    ValueTask<IReadOnlyList<UsbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default);

    ValueTask<IUsbTransport> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken = default);
}
