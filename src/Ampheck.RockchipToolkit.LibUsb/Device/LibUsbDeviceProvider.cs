using Ampheck.RockchipToolkit.LibUsb.Transport;
using Ampheck.RockchipToolkit.Transport;
using LibUsbDotNet.LibUsb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.LibUsb.Device;

/// <summary>Enumerates local USB devices through LibUsbDotNet.</summary>
public sealed class LibUsbDeviceProvider : IUsbDeviceProvider {
    private readonly LibUsbOptions _options;
    private readonly ILogger<LibUsbDeviceProvider> _logger;
    private readonly UsbContext _context;
    private bool _disposed;

    public LibUsbDeviceProvider(
        LibUsbOptions? options = null,
        ILogger<LibUsbDeviceProvider>? logger = null) {
        _options = options ?? new LibUsbOptions();
        _logger = logger ?? NullLogger<LibUsbDeviceProvider>.Instance;
        if (_options.TimeoutMilliseconds <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The LibUSB timeout must be positive.");
        }

        _context = new UsbContext();
    }

    public ValueTask<IReadOnlyList<UsbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Enumerating USB devices through LibUSB");

        var devices = new List<UsbDeviceInfo>();
        foreach (var device in _context.List()) {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = device.Info;
            var mode = DetectMode(
                descriptor.VendorId,
                descriptor.ProductId,
                descriptor.Usb);
            if (mode == RockchipUsbMode.None) {
                continue;
            }

            var locationId = device is UsbDevice usbDevice
                ? usbDevice.LocationId.ToString()
                : null;
            devices.Add(new UsbDeviceInfo(
                descriptor.VendorId,
                descriptor.ProductId,
                descriptor.Usb,
                mode,
                SerialNumber: descriptor.SerialNumber,
                PlatformId: locationId));
        }

        _logger.LogDebug(
            "LibUSB found {DeviceCount} RockUSB device(s)",
            devices.Count);
        return ValueTask.FromResult<IReadOnlyList<UsbDeviceInfo>>(
            devices.AsReadOnly());
    }

    public ValueTask<IUsbTransport> OpenAsync(
        UsbDeviceInfo device,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "Opening LibUSB device {VendorId:X4}:{ProductId:X4} " +
            "at {PlatformId}",
            device.VendorId,
            device.ProductId,
            device.PlatformId ?? "unknown");

        var handle = _context.Find(candidate =>
            Matches(candidate, device));
        if (handle is null) {
            throw new IOException(
                $"Unable to open USB device " +
                $"{device.VendorId:X4}:{device.ProductId:X4}.");
        }

        try {
            _logger.LogInformation(
                "Opened LibUSB device {VendorId:X4}:{ProductId:X4}",
                device.VendorId,
                device.ProductId);
            return ValueTask.FromResult<IUsbTransport>(
                new LibUsbTransport(handle, device, _options));
        } catch {
            handle.Close();
            throw;
        }
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _logger.LogDebug("Disposing LibUSB device provider");
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    private static bool Matches(
        IUsbDevice candidate,
        UsbDeviceInfo device) {
        if (candidate.VendorId != device.VendorId ||
            candidate.ProductId != device.ProductId) {
            return false;
        }

        if (device.PlatformId is null ||
            candidate is not UsbDevice usbDevice) {
            return true;
        }

        return string.Equals(
            usbDevice.LocationId.ToString(),
            device.PlatformId,
            StringComparison.Ordinal);
    }

    private static RockchipUsbMode DetectMode(
        ushort vendorId,
        ushort productId,
        ushort usbVersion) {
        if (!RockchipUsbCatalog.IsRockUsbDevice(
                vendorId,
                productId)) {
            return RockchipUsbMode.None;
        }

        return (usbVersion & 1) == 0
            ? RockchipUsbMode.MaskRom
            : RockchipUsbMode.Loader;
    }

}
