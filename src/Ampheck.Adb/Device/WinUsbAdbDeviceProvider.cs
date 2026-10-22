using Ampheck.Adb.Connection;
using Ampheck.Adb.Usb;
using Ampheck.Adb.Usb.WinUsb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.Adb.Device;

public sealed class WinUsbAdbDeviceProvider : IAdbDeviceProvider {
    private readonly AdbUsbOptions _options;
    private readonly ILogger<WinUsbAdbDeviceProvider> _logger;
    private bool _disposed;

    public WinUsbAdbDeviceProvider(
        AdbUsbOptions? options = null,
        ILogger<WinUsbAdbDeviceProvider>? logger = null) {
        _options = options ?? new AdbUsbOptions();
        _logger = logger ?? NullLogger<WinUsbAdbDeviceProvider>.Instance;
        if (_options.TimeoutMilliseconds <= 0 ||
            _options.MaxPayloadBytes is 0 or > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The ADB USB options are invalid.");
        }
    }

    public ValueTask<IReadOnlyList<AdbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default) {
        EnsureWindows();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var devices = new List<AdbDeviceInfo>();
        var paths = WinUsbDeviceEnumerator.Enumerate(cancellationToken);
        _logger.LogDebug(
            "WinUSB enumerated {DevicePathCount} USB device interface(s)",
            paths.Count);
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths) {
            cancellationToken.ThrowIfCancellationRequested();
            using var device = WinUsbDeviceProbe.TryOpen(path, _logger);
            if (device is null) {
                continue;
            }

            var info = new AdbDeviceInfo(
                device.VendorId,
                device.ProductId,
                device.SerialNumber,
                path);
            if (seen.Add(GetDeviceKey(info))) {
                devices.Add(info);
            }
        }

        _logger.LogDebug(
            "WinUSB found {DeviceCount} ADB device(s)",
            devices.Count);
        return ValueTask.FromResult<IReadOnlyList<AdbDeviceInfo>>(
            devices.AsReadOnly());
    }

    public ValueTask<AdbConnection> OpenAsync(
        AdbDeviceInfo device,
        CancellationToken cancellationToken = default) {
        EnsureWindows();
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(device.PlatformId)) {
            throw new ArgumentException(
                "The device was not produced by WinUsbAdbDeviceProvider.",
                nameof(device));
        }

        var opened = WinUsbDeviceProbe.TryOpen(device.PlatformId)
            ?? throw new IOException(
                $"Unable to open WinUSB ADB device " +
                $"{device.VendorId:X4}:{device.ProductId:X4}.");
        try {
            if (opened.VendorId != device.VendorId ||
                opened.ProductId != device.ProductId) {
                throw new IOException(
                    "The WinUSB device identity changed before opening.");
            }

            return ValueTask.FromResult(new AdbConnection(
                new WinUsbAdbTransport(opened, _options),
                _options.MaxPayloadBytes));
        } catch {
            opened.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync() {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private static void EnsureWindows() {
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(
                "The WinUSB ADB backend requires Windows.");
        }
    }

    private static string GetDeviceKey(AdbDeviceInfo device) {
        var identity = device.SerialNumber;
        if (string.IsNullOrWhiteSpace(identity)) {
            var interfaceIndex = device.PlatformId?.LastIndexOf(
                "#{",
                StringComparison.Ordinal) ?? -1;
            identity = interfaceIndex > 0
                ? device.PlatformId![..interfaceIndex]
                : device.PlatformId;
        }

        return $"{device.VendorId:X4}:{device.ProductId:X4}:{identity}";
    }
}
