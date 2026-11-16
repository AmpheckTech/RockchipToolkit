namespace Ampheck.RockchipToolkit.Adb.Model;

public sealed record AdbDeviceDescriptor {
    public required string SerialNumber { get; init; }
    public required string State { get; init; }
    public string? Product { get; init; }
    public string? Model { get; init; }
    public string? DeviceName { get; init; }
}
