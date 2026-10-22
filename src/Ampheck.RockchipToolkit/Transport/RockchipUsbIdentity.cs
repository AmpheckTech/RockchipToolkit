namespace Ampheck.RockchipToolkit.Transport;

public sealed record RockchipUsbIdentity(
    ushort VendorId,
    ushort ProductId,
    string Family,
    string Description,
    bool IsRockUsb);
