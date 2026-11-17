namespace Ampheck.RockchipToolkit.Devices;

internal sealed record DeviceWriteOptions(
    uint Sector,
    string? BackupPath = null,
    bool EraseBeforeWrite = false,
    long? Length = null,
    long? SourceOffset = null
);
