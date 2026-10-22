namespace Ampheck.RockchipToolkit.Protocol;

/// <summary>Decoded information returned by the RockUSB chip-info command.</summary>
public sealed record RockchipChipInfo(
    string Identifier,
    string PlatformName,
    string RawHex);
