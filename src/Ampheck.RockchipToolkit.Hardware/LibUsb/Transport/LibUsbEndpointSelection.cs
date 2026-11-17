namespace Ampheck.RockchipToolkit.Hardware.LibUsb.Transport;

internal readonly record struct LibUsbEndpointSelection(
    byte InterfaceNumber,
    byte BulkIn,
    byte BulkOut
);
