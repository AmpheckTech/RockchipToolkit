using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class ServerCommandRunner {
    public static async Task<int> RunNodaemonAsync(
        CommandOptions options,
        CliCommandContext context) {
        var endpoint = ToolkitEndpoint.Parse(
            options.ServerListen ??
            throw new ArgumentException(
                "A server listen endpoint is required."));
        var token = options.ServerAccessToken ??
            Environment.GetEnvironmentVariable(
                "ARKT_SERVER_TOKEN");
        var instanceId = Guid.NewGuid().ToString("D");
        var hostName = string.IsNullOrWhiteSpace(
                options.ServerHostName)
            ? Environment.MachineName
            : options.ServerHostName;
        var idleSeconds = options.ServerIdleSeconds ?? 0;
        var limits = CreateTransferLimits(options);
        var serialHistoryKiB =
            options.ServerSerialHistoryKiB ?? 256;
        var stateStore =
            new LocalServerStateStore(context.StatePath);
        if (serialHistoryKiB <= 0) {
            throw new ArgumentException(
                "--serial-history-kib must be greater than zero.");
        }

        Console.WriteLine($"server instance: {instanceId}");
        Console.WriteLine($"server hostname: {hostName}");
        Console.WriteLine($"server endpoint: {endpoint}");
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) => {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += handler;
        try {
            await using var server = new ToolkitHost(
                new ToolkitHostOptions {
                    Endpoint = endpoint,
                    Token = token,
                    InstanceId = instanceId,
                    HostName = hostName,
                    AdbPort = AdbEnvironment.GetConfiguredPort(),
                    TransferLimits = limits,
                    SerialHistoryBytes =
                        checked(serialHistoryKiB * 1024),
                    IdleTimeout = idleSeconds <= 0
                        ? Timeout.InfiniteTimeSpan
                        : TimeSpan.FromSeconds(idleSeconds),
                    OnListening = stateStore.WriteCurrentProcess
                },
                context.LoggerFactory,
                context.LoggerFactory.CreateLogger<ToolkitHost>());
            context.Logger.LogInformation(
                "Toolkit server started at {Endpoint}",
                endpoint);
            await server.RunAsync(stop.Token)
                .ConfigureAwait(false);
            return 0;
        } finally {
            Console.CancelKeyPress -= handler;
        }
    }

    public static Task<int> StartAsync(CliCommandContext context) =>
        RunControlAsync(
            () => LocalServerManager.StartAsync(
                context.Verbose,
                context.Console));

    public static Task<int> StopAsync(CliCommandContext context) =>
        RunControlAsync(
            () => LocalServerManager.StopAsync(context.Console));

    public static Task<int> StatusAsync(CliCommandContext context) =>
        RunControlAsync(
            () => LocalServerManager.ShowStatusAsync(
                context.Console));

    public static Task<int> DiscoverAsync(
        int seconds,
        CliCommandContext context) =>
        ToolkitDiscoveryCommandRunner.RunAsync(
            TimeSpan.FromSeconds(
                seconds));

    private static async Task<int> RunControlAsync(
        Func<Task> action) {
        await action().ConfigureAwait(false);
        return 0;
    }

    private static ToolkitTransferLimits CreateTransferLimits(
        CommandOptions options) {
        return new ToolkitTransferLimits {
            MaximumBlobBytes = ToBytes(
                options.ServerMaxBlobMiB ?? 32 * 1024)
        };
    }

    private static long ToBytes(int mebibytes) {
        return mebibytes > 0
            ? checked((long)mebibytes * 1024 * 1024)
            : throw new ArgumentOutOfRangeException(
                nameof(mebibytes),
                "Server transfer limits must be greater than zero.");
    }
}
