using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Hardware.LibUsb.Device;
using Ampheck.RockchipToolkit.Transport.Usb;
#if ROCKUSB
using Ampheck.RockchipToolkit.Hardware.RockUsb.Configuration;
using Ampheck.RockchipToolkit.Hardware.RockUsb.Device;
#endif

namespace Ampheck.RockchipToolkit.Hardware;

public sealed class UsbDeviceProviderFactory : IUsbDeviceProviderFactory {
    public IUsbDeviceProvider Create(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId
    ) {
        return transport switch {
#if ROCKUSB
            ToolkitTransport.Auto =>
                CreateRockUsb(vendorId, productId),
            ToolkitTransport.RockUsb =>
                CreateRockUsb(vendorId, productId),
#else
            ToolkitTransport.Auto => CreateLibUsb(),
#endif
            ToolkitTransport.LibUsb => CreateLibUsb(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(transport),
                transport,
                "The selected transport is not supported.")
        };
    }

    private static IUsbDeviceProvider CreateLibUsb() =>
        new LibUsbDeviceProvider();

#if ROCKUSB
    private static IUsbDeviceProvider CreateRockUsb(
        ushort? vendorId,
        ushort? productId
    ) {
        return new RockUsbDeviceProvider(
            new RockUsbOptions {
                VendorId = vendorId ?? RockchipUsbCatalog.DefaultVendorId,
                ProductId = productId
            });
    }
#endif
}
