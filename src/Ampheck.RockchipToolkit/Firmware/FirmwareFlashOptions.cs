namespace Ampheck.RockchipToolkit.Firmware;

public sealed record FirmwareFlashOptions {
    public string? LoaderPath { get; init; }
    public bool ResetAfterFlash { get; init; } = true;
}
