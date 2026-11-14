namespace Ampheck.RockchipToolkit.Transport.Serial;

public sealed record SerialEvent(
    SerialEventKind Kind,
    ReadOnlyMemory<byte> Data,
    string? Message = null);
