namespace Ampheck.RockchipToolkit.Images.Update;

internal sealed record ExtractionEntry(
    RockchipUpdateEntry Entry,
    string Destination
);
