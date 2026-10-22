namespace Ampheck.RockchipToolkit.Transport;

public static class RockchipUsbInterfaceProfile {
    public const byte VendorSpecificClass = 0xFF;
    public const byte SubClass = 0x06;
    public const byte Protocol = 0x05;

    public static bool Matches(
        byte interfaceClass,
        byte interfaceSubClass,
        byte interfaceProtocol) {
        return interfaceClass == VendorSpecificClass &&
            interfaceSubClass == SubClass &&
            interfaceProtocol == Protocol;
    }
}
