using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Devices;

public sealed record DeviceDescriptor {
    public required ushort VendorId { get; init; }
    public required ushort ProductId { get; init; }
    public required ushort UsbVersion { get; init; }
    public required RockchipUsbMode Mode { get; init; }
    public string? SerialNumber { get; init; }
    public string? PlatformId { get; init; }
    public string? Description { get; init; }
}
