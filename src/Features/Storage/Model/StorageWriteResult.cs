namespace Ampheck.RockchipToolkit.Features.Storage.Model;

public sealed record StorageWriteResult {
    public string? BackupPath { get; init; }
    public long BytesWritten { get; init; }
}
