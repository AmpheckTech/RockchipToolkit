using System.Diagnostics;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class LocalServerManager {
    private static readonly TimeSpan StartupTimeout =
        TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(150);

    public static async Task EnsureRunningAsync(
        bool verbose,
        ConsoleRenderer console,
        CancellationToken cancellationToken = default) {
        var endpoint = ToolkitEndpoint.Parse(
            ToolkitServerDefaults.Endpoint);
        if (await ToolkitEndpointResolver.IsReachableAsync(
                endpoint,
                cancellationToken)
            .ConfigureAwait(false)) {
            return;
        }

        await StartAsync(verbose, console, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task StartAsync(
        bool verbose,
        ConsoleRenderer console,
        CancellationToken cancellationToken = default) {
        var endpoint = ToolkitEndpoint.Parse(
            ToolkitServerDefaults.Endpoint);
        if (await ToolkitEndpointResolver.IsReachableAsync(
                endpoint,
                cancellationToken)
            .ConfigureAwait(false)) {
            console.WriteStatus(
                $"Server already running at {endpoint}",
                complete: true);
            return;
        }

        var paths = LocalServerPathsFactory.Create();
        Directory.CreateDirectory(paths.Directory);
        var startInfo =
            LocalServerProcessFactory.Create(verbose, paths);
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "Unable to start the toolkit server process.");

        var deadline =
            DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline) {
            if (await ToolkitEndpointResolver.IsReachableAsync(
                    endpoint,
                    cancellationToken)
                .ConfigureAwait(false)) {
                console.WriteStatus(
                    $"Server started at {endpoint}",
                    complete: true);
                Console.Error.WriteLine($"log: {paths.LogPath}");
                return;
            }

            if (process.HasExited) {
                throw new InvalidOperationException(
                    $"Server exited with code {process.ExitCode}. " +
                    $"Log: {paths.LogPath}");
            }

            await Task.Delay(
                    PollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        TryKill(process);
        throw new TimeoutException(
            $"Server did not start within " +
            $"{StartupTimeout.TotalSeconds:F0} seconds. " +
            $"Log: {paths.LogPath}");
    }

    public static async Task StopAsync(
        ConsoleRenderer console,
        CancellationToken cancellationToken = default) {
        var paths = LocalServerPathsFactory.Create();
        var stateStore =
            new LocalServerStateStore(paths.StatePath);
        var process = stateStore.FindRunningServer();
        if (process is not null) {
            using (process) {
                TryKill(process);
                try {
                    await process.WaitForExitAsync(
                            cancellationToken)
                        .WaitAsync(
                            TimeSpan.FromSeconds(5),
                            cancellationToken)
                        .ConfigureAwait(false);
                } catch (TimeoutException) {
                }

                if (!process.HasExited) {
                    throw new InvalidOperationException(
                        $"Server PID {process.Id} is still running. " +
                        "Start and stop the daemon with the same user " +
                        "or elevated permissions.");
                }
            }

            stateStore.Delete();
            console.WriteStatus(
                "Server stopped",
                complete: true);
            return;
        }

        stateStore.Delete();
        var endpoint = ToolkitEndpoint.Parse(
            ToolkitServerDefaults.Endpoint);
        if (await ToolkitEndpointResolver.IsReachableAsync(
                endpoint,
                cancellationToken)
            .ConfigureAwait(false)) {
            throw new InvalidOperationException(
                $"Server at {endpoint} was not started by this " +
                "daemon manager. Stop it in its owning process.");
        }

        console.WriteStatus(
            "Server is not running",
            complete: true);
    }

    public static async Task ShowStatusAsync(
        ConsoleRenderer console,
        CancellationToken cancellationToken = default) {
        var paths = LocalServerPathsFactory.Create();
        var stateStore =
            new LocalServerStateStore(paths.StatePath);
        var endpoint = ToolkitEndpoint.Parse(
            ToolkitServerDefaults.Endpoint);
        var reachable =
            await ToolkitEndpointResolver.IsReachableAsync(
                    endpoint,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!reachable) {
            console.WriteStatus(
                "Server is not running",
                complete: true);
            return;
        }

        var process = stateStore.FindRunningServer();
        if (process is not null) {
            using (process) {
                console.WriteStatus(
                    $"Server running at {endpoint}, " +
                    $"PID {process.Id}",
                    complete: true);
            }
        } else {
            console.WriteStatus(
                $"Server running at {endpoint}",
                complete: true);
        }

        Console.Error.WriteLine($"log: {paths.LogPath}");
    }

    private static void TryKill(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (Exception error) when (
            error is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException) {
        }
    }

}
