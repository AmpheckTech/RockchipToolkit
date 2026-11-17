namespace Ampheck.RockchipToolkit.Protocol.RockUsb;

/// <summary>Decoded information returned by the RockUSB chip-info command.</summary>
public sealed record RockchipChipInfo(
    string Identifier,
    string PlatformName
);
