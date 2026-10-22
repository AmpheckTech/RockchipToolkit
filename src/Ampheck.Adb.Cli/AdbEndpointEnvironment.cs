using Ampheck.Adb.Server;

namespace Ampheck.Adb.Cli;

internal static class AdbEndpointEnvironment {
    public static AdbServerOptions Resolve() {
        var socket = Environment.GetEnvironmentVariable(
            "ADB_SERVER_SOCKET");
        if (string.IsNullOrWhiteSpace(socket)) {
            return new AdbServerOptions();
        }

        var value = socket.StartsWith(
            "tcp:",
            StringComparison.OrdinalIgnoreCase)
            ? socket["tcp:".Length..]
            : socket;
        var separator = value.LastIndexOf(':');
        if (separator < 0) {
            if (int.TryParse(value, out var port) &&
                port is > 0 and <= ushort.MaxValue) {
                return new AdbServerOptions {
                    Port = port
                };
            }
        } else if (int.TryParse(
                value[(separator + 1)..],
                out var parsedPort) &&
            parsedPort is > 0 and <= ushort.MaxValue) {
            var host = separator == 0
                ? "127.0.0.1"
                : value[..separator];
            return new AdbServerOptions {
                Host = host,
                Port = parsedPort
            };
        }

        throw new InvalidOperationException(
            $"Unsupported ADB_SERVER_SOCKET value '{socket}'.");
    }
}
