namespace Ampheck.RockchipToolkit.Adb.Model;

public enum AdbDeviceState {
    Offline,
    Unauthorized,
    Device,
    Recovery,
    Sideload,
    Bootloader,
    Unknown
}
