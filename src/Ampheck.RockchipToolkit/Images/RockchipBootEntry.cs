namespace Ampheck.RockchipToolkit.Images;

public sealed record RockchipBootEntry(
    BootEntryType Type,
    string Name,
    uint Offset,
    uint Size,
    uint Delay);
