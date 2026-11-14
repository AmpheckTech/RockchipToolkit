namespace Ampheck.RockchipToolkit.Images;

/// <summary>Byte-based progress for an image pack or unpack operation.</summary>
public readonly record struct RockchipImageProgress(long Current, long Total);
