using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;

internal static class SerialPortConnectionFactory {
    public static ISerialPortConnection Create(
        SerialPortOptions options
    ) {
#if WINDOWS
        return new WindowsSerialPortConnection(options);
#else
        return new LinuxSerialPortConnection(options);
#endif
    }
}
