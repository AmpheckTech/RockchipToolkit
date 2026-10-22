namespace Ampheck.RockchipToolkit.Protocol;

/// <summary>Decoded features returned by the RockUSB read-capability command.</summary>
public sealed record RockchipCapabilityInfo(
    RockchipCapabilityFlags Flags,
    string RawHex);
