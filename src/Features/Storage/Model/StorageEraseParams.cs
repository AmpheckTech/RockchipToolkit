using Ampheck.RockchipToolkit.Core.Devices;

namespace Ampheck.RockchipToolkit.Features.Storage.Model;

public sealed record StorageEraseParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderLocation { get; init; }
    public uint Sector { get; init; }
    public ushort Count { get; init; }
}
