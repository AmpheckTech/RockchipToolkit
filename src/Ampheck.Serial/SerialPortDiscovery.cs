namespace Ampheck.Serial;

internal static class SerialPortDiscovery {
    private static readonly string[] LinuxPatterns = [
        "ttyUSB*",
        "ttyACM*",
        "ttyS*",
        "rfcomm*"
    ];

    public static IReadOnlyList<string> List() {
#if WINDOWS
        return System.IO.Ports.SerialPort.GetPortNames()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
#else
        if (!OperatingSystem.IsLinux()) {
            return [];
        }

        var ports = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var pattern in LinuxPatterns) {
            try {
                foreach (var path in Directory.EnumerateFiles(
                    "/dev",
                    pattern)) {
                    ports.Add(path);
                }
            } catch (Exception error) when (
                error is IOException or UnauthorizedAccessException) {
            }
        }

        return ports
            .Order(StringComparer.Ordinal)
            .ToArray();
#endif
    }
}
