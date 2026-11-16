namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record DeviceChipInfoResult {
    public required string Identifier { get; init; }
    public required string PlatformName { get; init; }
}
