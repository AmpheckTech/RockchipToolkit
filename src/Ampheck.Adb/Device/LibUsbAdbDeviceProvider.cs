using Ampheck.Adb.Connection;
using Ampheck.Adb.Usb;
using LibUsbDotNet.LibUsb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.Adb.Device;

/// <summary>Discovers and opens ADB USB interfaces without an ADB server.</summary>
public sealed class LibUsbAdbDeviceProvider : IAdbDeviceProvider {
    private readonly AdbUsbOptions _options;
    private readonly ILogger<LibUsbAdbDeviceProvider> _logger;
    private readonly UsbContext _context;
    private bool _disposed;

    public LibUsbAdbDeviceProvider(
        AdbUsbOptions? options = null,
        ILogger<LibUsbAdbDeviceProvider>? logger = null) {
        _options = options ?? new AdbUsbOptions();
        _logger = logger ?? NullLogger<LibUsbAdbDeviceProvider>.Instance;
        if (_options.TimeoutMilliseconds <= 0 ||
            _options.MaxPayloadBytes is 0 or > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The ADB USB options are invalid.");
        }

        _context = new UsbContext();
    }

    public ValueTask<IReadOnlyList<AdbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var devices = new List<AdbDeviceInfo>();
        foreach (var device in _context.List()) {
            cancellationToken.ThrowIfCancellationRequested();
            if (AdbInterfaceSelector.TrySelect(device) is null) {
                continue;
            }

            var descriptor = device.Info;
            devices.Add(new AdbDeviceInfo(
                descriptor.VendorId,
                descriptor.ProductId,
                GetSerial(device, descriptor.SerialNumber),
                device is UsbDevice usbDevice
                    ? usbDevice.LocationId.ToString()
                    : null));
        }

        _logger.LogDebug(
            "LibUSB found {DeviceCount} ADB device(s)",
            devices.Count);
        return ValueTask.FromResult<IReadOnlyList<AdbDeviceInfo>>(
            devices.AsReadOnly());
    }

    public ValueTask<AdbConnection> OpenAsync(
        AdbDeviceInfo device,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var handle = _context.Find(candidate =>
            Matches(candidate, device));
        if (handle is null) {
            throw new IOException(
                $"Unable to open ADB device " +
                $"{device.VendorId:X4}:{device.ProductId:X4}.");
        }

        var selection = AdbInterfaceSelector.TrySelect(handle)
            ?? throw new IOException(
                "The selected USB device has no ADB interface.");
        try {
            var transport = new AdbUsbTransport(
                handle,
                selection,
                _options);
            return ValueTask.FromResult(new AdbConnection(
                transport,
                _options.MaxPayloadBytes));
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
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    private static bool Matches(
        IUsbDevice candidate,
        AdbDeviceInfo device) {
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

    private static string? GetSerial(
        IUsbDevice device,
        string? serialNumber) {
        if (!string.IsNullOrWhiteSpace(serialNumber)) {
            return serialNumber;
        }

        if (device is not UsbDevice usbDevice) {
            return null;
        }

        var ports = string.Join(".", usbDevice.PortNumbers);
        return ports.Length == 0
            ? $"usb:{usbDevice.BusNumber}"
            : $"usb:{usbDevice.BusNumber}-{ports}";
    }
}
