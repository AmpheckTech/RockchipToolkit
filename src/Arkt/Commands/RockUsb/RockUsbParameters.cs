using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.Arkt.Commands.RockUsb.Parser;
using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb;

internal class RockUsbParameters {
#if ROCKUSB
    [CommandParam(
        ["--transport", "-t"],
        Description = "auto, rockusb, or libusb.",
        Parser = typeof(ToolkitTransportParser),
        Choices = ["auto", "rockusb", "libusb"])]
#else
    [CommandParam(
        ["--transport", "-t"],
        Description = "auto or libusb.",
        Parser = typeof(ToolkitTransportParser),
        Choices = ["auto", "libusb"])]
#endif
    public ToolkitTransport Transport { get; init; } =
        ToolkitTransport.Auto;

    [CommandParam(
        ["--vid"],
        Description = "USB vendor ID.",
        Parser = typeof(HexUInt16Parser))]
    public ushort? VendorId { get; init; }

    [CommandParam(
        ["--pid"],
        Description = "USB product ID.",
        Parser = typeof(HexUInt16Parser))]
    public ushort? ProductId { get; init; }

    [CommandParam(
        ["--verbose", "-v"],
        Description = "Enable informational and debug logs.")]
    public bool Verbose { get; init; }
}
