namespace Ampheck.Serial;

public enum SerialEventKind {
    Data,
    Connected,
    Disconnected,
    Reconnected,
    Overflow
}
