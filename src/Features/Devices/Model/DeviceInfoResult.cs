namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record DeviceInfoResult {
    public required string ChipIdentifier { get; init; }
    public required string PlatformName { get; init; }
    public required string Manufacturer { get; init; }
    public required byte ManufacturerCode { get; init; }
    public required long CapacityBytes { get; init; }
    public required uint BlockSizeBytes { get; init; }
    public required uint PageSizeBytes { get; init; }
    public required uint BlockCount { get; init; }
    public required byte EccBits { get; init; }
    public required byte AccessTime { get; init; }
    public required byte ChipSelectMask { get; init; }
}
