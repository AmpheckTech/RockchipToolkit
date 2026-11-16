namespace Ampheck.RockchipToolkit.Features.Devices.Model;

public sealed record DeviceWriteOptions(
    uint Sector,
    string? BackupPath = null,
    bool EraseBeforeWrite = false,
    long? Length = null,
    long? SourceOffset = null
);
