namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record DeviceListResult {
    public required DeviceDescriptor[] Devices { get; init; }
}
