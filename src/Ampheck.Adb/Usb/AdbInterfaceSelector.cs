using LibUsbDotNet.LibUsb;

namespace Ampheck.Adb.Usb;

internal static class AdbInterfaceSelector {
    private const byte AdbSubclass = 0x42;
    private const byte AdbProtocol = 0x01;

    public static AdbInterfaceSelection? TrySelect(
        IUsbDevice device) {
        foreach (var configuration in device.Configs) {
            foreach (var setting in configuration.Interfaces) {
                if ((byte)setting.Class != 0xFF ||
                    (byte)setting.SubClass != AdbSubclass ||
                    (byte)setting.Protocol != AdbProtocol) {
                    continue;
                }

                byte? bulkIn = null;
                byte? bulkOut = null;
                foreach (var endpoint in setting.Endpoints) {
                    if ((endpoint.Attributes & 0x03) != 0x02) {
                        continue;
                    }

                    if ((endpoint.EndpointAddress & 0x80) != 0) {
                        bulkIn ??= endpoint.EndpointAddress;
                    } else {
                        bulkOut ??= endpoint.EndpointAddress;
                    }
                }

                if (bulkIn is not null && bulkOut is not null) {
                    return new AdbInterfaceSelection(
                        checked((byte)setting.Number),
                        bulkIn.Value,
                        bulkOut.Value);
                }
            }
        }

        return null;
    }
}
