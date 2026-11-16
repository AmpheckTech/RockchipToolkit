namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record DeviceDescriptor {
    public required ushort VendorId { get; init; }
    public required ushort ProductId { get; init; }
    public required ushort UsbVersion { get; init; }
    public required string Mode { get; init; }
    public string? SerialNumber { get; init; }
    public string? PlatformId { get; init; }
    public string? Description { get; init; }
}
