namespace Ampheck.Adb.Usb;

internal static class AdbUsbInterfaceProfile {
    public const byte VendorSpecificClass = 0xFF;
    public const byte SubClass = 0x42;
    public const byte Protocol = 0x01;

    public static bool Matches(
        byte interfaceClass,
        byte interfaceSubClass,
        byte interfaceProtocol) {
        return interfaceClass == VendorSpecificClass &&
            interfaceSubClass == SubClass &&
            interfaceProtocol == Protocol;
    }
}
