using Ampheck.RockchipToolkit.Core.Devices;

namespace Ampheck.RockchipToolkit.Features.Storage.Model;

public sealed record StorageWriteParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderLocation { get; init; }
    public required string InputLocation { get; init; }
    public uint Sector { get; init; }
    public long? Length { get; init; }
    public long? SourceOffset { get; init; }
    public bool EraseBeforeWrite { get; init; } = true;
    public string? BackupPath { get; init; }
}
