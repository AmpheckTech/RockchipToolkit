using Ampheck.RockchipToolkit.LibUsb.Device;
using Ampheck.RockchipToolkit.RockUsb.Configuration;
using Ampheck.RockchipToolkit.RockUsb.Device;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Devices;

internal static class DeviceProviderFactory {
    public static IUsbDeviceProvider Create(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId,
        ILoggerFactory loggerFactory) {
        if (transport == ToolkitTransport.RockUsb &&
            !OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(
                "The RockUSB transport is only available on Windows.");
        }

        return transport switch {
            ToolkitTransport.Auto when OperatingSystem.IsWindows() =>
                CreateRockUsb(
                    vendorId,
                    productId,
                    loggerFactory),
            ToolkitTransport.Auto =>
                CreateLibUsb(loggerFactory),
            ToolkitTransport.RockUsb =>
                CreateRockUsb(
                    vendorId,
                    productId,
                    loggerFactory),
            ToolkitTransport.LibUsb =>
                CreateLibUsb(loggerFactory),
            _ => throw new ArgumentOutOfRangeException(
                nameof(transport),
                transport,
                "The selected transport is not supported.")
        };
    }

    private static IUsbDeviceProvider CreateLibUsb(
        ILoggerFactory loggerFactory) {
        return new LibUsbDeviceProvider(
            logger: loggerFactory.CreateLogger<LibUsbDeviceProvider>());
    }

    private static IUsbDeviceProvider CreateRockUsb(
        ushort? vendorId,
        ushort? productId,
        ILoggerFactory loggerFactory) {
        return new RockUsbDeviceProvider(
            new RockUsbOptions {
                VendorId = vendorId ?? 0x2207,
                ProductId = productId
            },
            loggerFactory.CreateLogger<RockUsbDeviceProvider>());
    }
}
