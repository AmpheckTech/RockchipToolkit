namespace Ampheck.Adb.Device;

/// <summary>USB identity of an Android Debug Bridge device.</summary>
public sealed record AdbDeviceInfo(
    ushort VendorId,
    ushort ProductId,
    string? SerialNumber,
    string? PlatformId);
