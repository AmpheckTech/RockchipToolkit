namespace Ampheck.RockchipToolkit.LibUsb.Transport;

/// <summary>Endpoint and interface selection for RockUSB devices.</summary>
public sealed record LibUsbOptions {
    public byte InterfaceNumber { get; init; }
    public byte BulkInEndpoint { get; init; }
    public byte BulkOutEndpoint { get; init; }
    public int TimeoutMilliseconds { get; init; } = 5000;
    public bool ClaimInterface { get; init; } = true;
}
