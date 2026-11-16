using Ampheck.RockchipToolkit.Core.Devices;

namespace Ampheck.RockchipToolkit.Features.Firmware.Model;

public sealed record FirmwareFlashParams {
    public required DeviceTarget Target { get; init; }
    public required string FirmwareLocation { get; init; }
    public string? LoaderLocation { get; init; }
    public bool ResetAfterFlash { get; init; } = true;
}
