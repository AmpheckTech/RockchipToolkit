using Ampheck.Adb.Usb;
using Microsoft.Extensions.Logging;

namespace Ampheck.Adb.Device;

public static class AdbDeviceProviderFactory {
    public static IAdbDeviceProvider Create(
        ILoggerFactory loggerFactory,
        AdbUsbOptions? options = null) {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (OperatingSystem.IsWindows()) {
            return new WinUsbAdbDeviceProvider(
                options,
                logger: loggerFactory.CreateLogger<
                    WinUsbAdbDeviceProvider>());
        }

        if (OperatingSystem.IsLinux()) {
            return new LibUsbAdbDeviceProvider(
                options,
                logger: loggerFactory.CreateLogger<
                    LibUsbAdbDeviceProvider>());
        }

        throw new PlatformNotSupportedException(
            "ADB is supported on Windows through WinUSB and on Linux through LibUSB.");
    }
}
