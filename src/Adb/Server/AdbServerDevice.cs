namespace Ampheck.RockchipToolkit.Adb.Server;

public sealed record AdbServerDevice(
    string SerialNumber,
    string State,
    string? Product,
    string? Model,
    string? Device
);
