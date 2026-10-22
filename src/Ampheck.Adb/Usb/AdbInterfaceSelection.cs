namespace Ampheck.Adb.Usb;

internal readonly record struct AdbInterfaceSelection(
    byte InterfaceNumber,
    byte BulkInEndpoint,
    byte BulkOutEndpoint);
