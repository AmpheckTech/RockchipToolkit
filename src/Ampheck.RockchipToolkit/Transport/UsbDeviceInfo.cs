namespace Ampheck.RockchipToolkit.Transport;

/// <summary>Immutable information reported by a USB enumerator.</summary>
public sealed record UsbDeviceInfo(
    ushort VendorId,
    ushort ProductId,
    ushort UsbVersion,
    RockchipUsbMode Mode,
    string? SerialNumber = null,
    string? PlatformId = null);
