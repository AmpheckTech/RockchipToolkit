namespace Ampheck.RockchipToolkit.Firmware;

public sealed record FirmwareFlashOptions {
    public string? LoaderPath { get; init; }
    public string? BootloaderPath { get; init; }
    public string? IdbPath { get; init; }
    public bool ResetAfterFlash { get; init; } = true;
}
