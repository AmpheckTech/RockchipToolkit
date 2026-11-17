namespace Ampheck.RockchipToolkit.Transport.Usb;

public readonly record struct UsbControlRequest(
    byte RequestType,
    byte Request,
    ushort Value,
    ushort Index,
    uint Length
);
