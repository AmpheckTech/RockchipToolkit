namespace Ampheck.RockchipToolkit.Core.Devices;

public sealed record DeviceTarget {
    public ToolkitTransport Transport { get; init; } =
        ToolkitTransport.Auto;
    public ushort? VendorId { get; init; }
    public ushort? ProductId { get; init; }
}
