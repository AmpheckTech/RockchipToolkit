using Ampheck.RockchipToolkit.Core.Devices;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb;

internal static class RockUsbTargetFactory {
    public static DeviceTarget Create(RockUsbParameters parameters) =>
        new() {
            Transport = parameters.Transport,
            VendorId = parameters.VendorId,
            ProductId = parameters.ProductId
        };
}
