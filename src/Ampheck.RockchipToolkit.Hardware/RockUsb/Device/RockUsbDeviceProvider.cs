using Ampheck.RockchipToolkit.Hardware.RockUsb.Configuration;
using Ampheck.RockchipToolkit.Hardware.RockUsb.Transport;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Hardware.RockUsb.Device;

/// <summary>Discovers Rockchip devices bound to the DriverAssistant WDM driver.</summary>
public sealed class RockUsbDeviceProvider : IUsbDeviceProvider {
    private readonly RockUsbOptions _options;

    public RockUsbDeviceProvider(RockUsbOptions? options = null) {
        _options = options ?? new RockUsbOptions();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<UsbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default
    ) {
        EnsureWindows();
        return ValueTask.FromResult(
            RockUsbDeviceEnumerator.Enumerate(
                _options,
                cancellationToken));
    }

    public ValueTask<IUsbTransport> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken = default
    ) {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(device.PlatformId)) {
            throw new ArgumentException(
                "The device was not produced by RockUsbDeviceProvider.",
                nameof(device));
        }

        return ValueTask.FromResult<IUsbTransport>(
            RockUsbTransport.Open(device, _options));
    }

    private static void EnsureWindows() {
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(
                "The RockUsb backend requires Windows and rockusb.sys.");
        }
    }
}
