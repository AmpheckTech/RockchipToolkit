namespace Ampheck.Adb;

public enum AdbDeviceState {
    Offline,
    Unauthorized,
    Device,
    Recovery,
    Sideload,
    Bootloader,
    Unknown
}
