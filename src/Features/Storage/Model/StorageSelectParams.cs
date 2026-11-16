using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;

namespace Ampheck.RockchipToolkit.Features.Storage.Model;

public sealed record StorageSelectParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderLocation { get; init; }
    public required StorageType Storage { get; init; }
}
