namespace Ampheck.RockchipToolkit.Transport.LibUsb.Transport;

internal readonly record struct LibUsbEndpointSelection(
    byte InterfaceNumber,
    byte BulkIn,
    byte BulkOut);
