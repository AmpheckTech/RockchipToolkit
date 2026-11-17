using Ampheck.RockchipToolkit.Images.Rkfw;

namespace Ampheck.RockchipToolkit.Images.Update;

internal sealed record RockchipUpdateImageReadResult(
    RockchipUpdateImageKind Kind,
    RockchipImageHeader? ContainerHeader,
    uint Version,
    string MachineModel,
    string MachineId,
    string Manufacturer,
    IReadOnlyList<RockchipUpdateEntry> Entries
);
