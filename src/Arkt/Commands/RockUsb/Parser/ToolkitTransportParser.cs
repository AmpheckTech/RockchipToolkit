using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb.Parser;

internal sealed class ToolkitTransportParser :
    ICommandValueParser<ToolkitTransport> {
    public ToolkitTransport Parse(string name, string value) =>
        value.ToLowerInvariant() switch {
            "rockusb" => ToolkitTransport.RockUsb,
            "libusb" => ToolkitTransport.LibUsb,
            _ => ToolkitTransport.Auto
        };
}
