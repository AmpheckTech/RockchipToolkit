namespace Ampheck.RockchipToolkit.Adb.Model;

public sealed record AdbDevicesResult {
    public required AdbDeviceDescriptor[] Devices { get; init; }
}
