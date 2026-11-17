using Ampheck.RockchipToolkit.Devices;

namespace Ampheck.RockchipToolkit.Transport.Usb;

public interface IUsbDeviceProviderFactory {
    IUsbDeviceProvider Create(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId);
}
