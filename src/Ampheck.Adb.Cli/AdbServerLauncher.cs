using System.Diagnostics;
using System.Net;
using Ampheck.Adb.Server;

namespace Ampheck.Adb.Cli;

internal static class AdbServerLauncher {
    private static readonly TimeSpan StartupTimeout =
        TimeSpan.FromSeconds(5);

    public static async Task EnsureRunningAsync(
        AdbServerOptions options,
        CancellationToken cancellationToken) {
        if (await AdbServerClient.IsAvailableAsync(
                cancellationToken,
                options)
            .ConfigureAwait(false)) {
            return;
        }

        if (!IsLocalHost(options.Host)) {
            throw new InvalidOperationException(
                $"No ADB server is running at {options.Host}:{options.Port}.");
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The adbcli executable path is unavailable.");
        var frameworkDependent = Path
            .GetFileNameWithoutExtension(executable)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        var assemblyPath = frameworkDependent
            ? Environment.GetCommandLineArgs()
                .FirstOrDefault()
            : null;
        if (frameworkDependent && string.IsNullOrWhiteSpace(assemblyPath)) {
            throw new InvalidOperationException(
                "The adbcli assembly path is unavailable.");
        }

        var startInfo = new ProcessStartInfo {
            FileName = executable,
            Arguments = frameworkDependent
                ? $"\"{assemblyPath}\" -H {options.Host} " +
                    $"-P {options.Port} server nodaemon"
                : $"-H {options.Host} -P {options.Port} " +
                    "server nodaemon",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start the ADB server process.");

        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await AdbServerClient.IsAvailableAsync(
                    cancellationToken,
                    options)
                .ConfigureAwait(false)) {
                return;
            }

            await Task.Delay(100, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"ADB server did not start at {options.Host}:{options.Port}.");
    }

    private static bool IsLocalHost(string host) {
        return host.Equals(
                "localhost",
                StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out var address) &&
            IPAddress.IsLoopback(address);
    }
}
