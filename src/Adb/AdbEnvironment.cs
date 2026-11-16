using Ampheck.RockchipToolkit.Adb.Model;

namespace Ampheck.RockchipToolkit.Adb;

internal static class AdbEnvironment {
    public static int GetConfiguredPort() =>
        ParsePort(Environment.GetEnvironmentVariable(
            "ARKT_ADB_PORT"));

    private static int ParsePort(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return AdbDefaults.ServerPort;
        }

        return int.TryParse(value, out var port) &&
            port is > 0 and <= ushort.MaxValue
                ? port
                : throw new InvalidOperationException(
                    $"Invalid ARKT_ADB_PORT value '{value}'.");
    }
}
