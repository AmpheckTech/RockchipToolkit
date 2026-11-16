namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record DeviceCapabilityResult {
    public required ulong Flags { get; init; }
}
