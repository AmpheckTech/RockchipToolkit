using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.HardwareTransport.Backends.LibUsb.Device;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
#if ROCKUSB
using Ampheck.RockchipToolkit.HardwareTransport.Backends.RockUsb.Configuration;
using Ampheck.RockchipToolkit.HardwareTransport.Backends.RockUsb.Device;
#endif
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.HardwareTransport.Backends;

public sealed class UsbDeviceProviderFactory {
    public IUsbDeviceProvider Create(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId,
        ILoggerFactory loggerFactory
    ) {
        return transport switch {
#if ROCKUSB
            ToolkitTransport.Auto =>
                CreateRockUsb(vendorId, productId, loggerFactory),
            ToolkitTransport.RockUsb =>
                CreateRockUsb(vendorId, productId, loggerFactory),
#else
            ToolkitTransport.Auto => CreateLibUsb(loggerFactory),
#endif
            ToolkitTransport.LibUsb =>
                CreateLibUsb(loggerFactory),
            _ => throw new ArgumentOutOfRangeException(
                nameof(transport),
                transport,
                "The selected transport is not supported.")
        };
    }

    private static IUsbDeviceProvider CreateLibUsb(
        ILoggerFactory loggerFactory
    ) {
        return new LibUsbDeviceProvider(
            logger: loggerFactory.CreateLogger<LibUsbDeviceProvider>());
    }

#if ROCKUSB
    private static IUsbDeviceProvider CreateRockUsb(
        ushort? vendorId,
        ushort? productId,
        ILoggerFactory loggerFactory
    ) {
        return new RockUsbDeviceProvider(
            new RockUsbOptions {
                VendorId = vendorId ??
                    RockchipUsbCatalog.DefaultVendorId,
                ProductId = productId
            },
            loggerFactory.CreateLogger<RockUsbDeviceProvider>());
    }
#endif
}
