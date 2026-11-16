using Ampheck.RockchipToolkit.Core.Devices;

namespace Ampheck.RockchipToolkit.Features.Storage.Model;

public sealed record StorageReadParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderLocation { get; init; }
    public required string OutputPath { get; init; }
    public uint Sector { get; init; }
    public ushort Count { get; init; } = 1;
}
