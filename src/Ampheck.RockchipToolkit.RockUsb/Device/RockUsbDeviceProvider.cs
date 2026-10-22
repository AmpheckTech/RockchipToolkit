using Ampheck.RockchipToolkit.RockUsb.Configuration;
using Ampheck.RockchipToolkit.RockUsb.Transport;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.RockUsb.Device;

/// <summary>Discovers Rockchip devices bound to the DriverAssistant WDM driver.</summary>
public sealed class RockUsbDeviceProvider : IUsbDeviceProvider {
    private readonly RockUsbOptions _options;
    private readonly ILogger<RockUsbDeviceProvider> _logger;

    public RockUsbDeviceProvider(
        RockUsbOptions? options = null,
        ILogger<RockUsbDeviceProvider>? logger = null) {
        _options = options ?? new RockUsbOptions();
        _logger = logger ?? NullLogger<RockUsbDeviceProvider>.Instance;
    }

    public ValueTask DisposeAsync() {
        _logger.LogDebug("Disposing RockUsb device provider");
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<UsbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default) {
        EnsureWindows();
        _logger.LogDebug("Enumerating USB devices through RockUsb");
        var devices = RockUsbDeviceEnumerator.Enumerate(
            _options,
            cancellationToken);
        _logger.LogDebug(
            "RockUsb found {DeviceCount} device(s)",
            devices.Count);
        return ValueTask.FromResult(devices);
    }

    public ValueTask<IUsbTransport> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken = default) {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(device.PlatformId)) {
            throw new ArgumentException(
                "The device was not produced by RockUsbDeviceProvider.",
                nameof(device));
        }

        _logger.LogInformation(
            "Opening RockUsb device {VendorId:X4}:{ProductId:X4} at {PlatformId}",
            device.VendorId,
            device.ProductId,
            device.PlatformId);
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
