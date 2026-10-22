namespace Ampheck.Adb.Server;

public sealed record AdbServerDevice(
    string SerialNumber,
    string State,
    string? Product,
    string? Model,
    string? Device);
