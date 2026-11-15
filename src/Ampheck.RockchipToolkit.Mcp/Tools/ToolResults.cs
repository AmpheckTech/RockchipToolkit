namespace Ampheck.RockchipToolkit.Mcp.Tools;

public sealed record StorageWriteToolResult {
    public long BytesWritten { get; init; }
    public string? BackupPath { get; init; }
}
