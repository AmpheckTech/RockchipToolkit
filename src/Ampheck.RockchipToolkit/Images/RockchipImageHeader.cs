namespace Ampheck.RockchipToolkit.Images;

public sealed record RockchipImageHeader(
    uint Version,
    uint MergeVersion,
    ushort HeaderSize,
    uint BootOffset,
    uint BootSize,
    uint FirmwareOffset,
    long FirmwareSize,
    uint SupportedChip,
    bool Signed,
    RockchipImageKind Kind);
