namespace Ampheck.RockchipToolkit.Storage;

public sealed record StorageWriteOptions {
    public long? Length { get; init; }
    public long? SourceOffset { get; init; }
    public bool EraseBeforeWrite { get; init; } = true;
    public string? BackupPath { get; init; }
}
