using Ampheck.Adb.Server;

namespace Ampheck.Adb.Cli;

internal static class AdbClientFactory {
    public static async Task<AdbClient> CreateAsync(
        ClientSettings settings,
        CancellationToken cancellationToken) {
        var server = new AdbServerOptions {
            Host = settings.Host,
            Port = settings.Port
        };
        if (!settings.Direct) {
            await AdbServerLauncher.EnsureRunningAsync(
                    server,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (settings.Verbose) {
            Console.Error.WriteLine(
                settings.Direct
                    ? "using direct USB ADB transport"
                    : $"using ADB server {settings.Host}:{settings.Port}");
        }

        return new AdbClient(new AdbClientOptions {
            Backend = settings.Direct
                ? AdbBackendPreference.Usb
                : AdbBackendPreference.AdbServer,
            Server = server
        });
    }
}
