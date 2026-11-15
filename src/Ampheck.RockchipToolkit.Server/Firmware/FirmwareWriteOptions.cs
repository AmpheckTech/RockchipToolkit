namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed record FirmwareWriteOptions(
    uint Sector,
    string? BackupPath = null,
    bool EraseBeforeWrite = false,
    long? Length = null,
    long? SourceOffset = null);
