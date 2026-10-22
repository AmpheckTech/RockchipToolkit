using Ampheck.Adb;
using Ampheck.Adb.Server;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal sealed record AdbEnvironment(
    AdbClientOptions Client,
    ToolkitEndpoint? Toolkit) {
    public static AdbEnvironment Resolve() {
        var server = Environment.GetEnvironmentVariable(
            "ARKT_SERVER");
        if (string.IsNullOrWhiteSpace(server)) {
            return new AdbEnvironment(
                new AdbClientOptions(),
                Toolkit: null);
        }

        var endpoint = ToolkitEndpoint.Parse(server);
        return new AdbEnvironment(
            new AdbClientOptions {
                Backend = AdbBackendPreference.AdbServer,
                Server = new AdbServerOptions {
                    Host = endpoint.Host,
                    Port = GetConfiguredPort()
                }
            },
            endpoint);
    }

    public static int GetConfiguredPort() =>
        ParsePort(Environment.GetEnvironmentVariable(
            "ARKT_ADB_PORT"));

    private static int ParsePort(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return 5037;
        }

        return int.TryParse(value, out var port) &&
            port is > 0 and <= ushort.MaxValue
                ? port
                : throw new InvalidOperationException(
                    $"Invalid ARKT_ADB_PORT value '{value}'.");
    }
}
