using System.Diagnostics;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class Application {
    public static async Task<int> RunAsync(string[] args) {
        var daemon = DaemonLaunchContext.Parse(args);
        using var daemonLog = DaemonLogFile.Open(
            daemon.LogPath);
        args = daemon.Arguments;
        var verboseRequested = HasOption(args, "--verbose", "-v");
        var serverProcess =
            args.Length >= 2 &&
            args[0].Equals(
                CommandNames.Server,
                StringComparison.OrdinalIgnoreCase) &&
            args[1].Equals(
                "nodaemon",
                StringComparison.OrdinalIgnoreCase);
        using var console = new ConsoleRenderer();
        using var loggerFactory = Logging.Create(
            verboseRequested,
            serverProcess,
            console);
        var logger = loggerFactory.CreateLogger(
            "Ampheck.RockchipToolkit.Application");

        try {
            if (args.Length == 0) {
                WriteMotd(console);
                CommandLine.PrintHelp(["--help"], Console.Out);
                return 0;
            }

            if (args is ["--version"]) {
                WriteMotd(console);
                CommandLine.PrintVersion(Console.Out);
                return 0;
            }

            if (CommandLine.TryPrintCompletion(args, Console.Out)) {
                return 0;
            }

            if (args.Length == 1 &&
                (IsHelpCommand(args[0]) ||
                    IsRootHelpOption(args[0]))) {
                WriteMotd(console);
                CommandLine.PrintHelp(["--help"], Console.Out);
                return 0;
            }

            if (IsHelpCommand(args[0])) {
                CommandLine.PrintHelp(
                    [.. args.Skip(1), "--help"],
                    Console.Out);
                return 0;
            }

            if (HasOption(args, "--help", "-h", "-?")) {
                CommandLine.PrintHelp(args, Console.Out);
                return 0;
            }

            var invocation = CommandLine.Parse(args, Console.Out);
            if (invocation is null) {
                return 0;
            }

            var command = invocation.Command;
            var options = invocation.Options;
            if (command == CommandNames.Completion) {
                Console.Out.Write(CompletionScripts.Get(
                    options.CompletionShell!));
                Console.Out.Flush();
                return 0;
            }

            logger.LogDebug(
                "Starting command {Command} with transport {Transport}",
                command,
                options.Transport);

            var startedAt = Stopwatch.GetTimestamp();
            if (command == CommandNames.Discover) {
                return await ToolkitDiscoveryCommandRunner.RunAsync(
                        options)
                    .ConfigureAwait(false);
            }

            if (command == CommandNames.ServerNodaemon) {
                await RunServerAsync(
                        options,
                        loggerFactory,
                        logger,
                        daemon.StatePath)
                    .ConfigureAwait(false);
                return 0;
            }

            if (command == CommandNames.ServerStart) {
                await LocalServerManager.StartAsync(
                        verboseRequested,
                        console)
                    .ConfigureAwait(false);
                return 0;
            }

            if (command == CommandNames.ServerStop) {
                await LocalServerManager.StopAsync(console)
                    .ConfigureAwait(false);
                return 0;
            }

            if (command == CommandNames.ServerStatus) {
                await LocalServerManager.ShowStatusAsync(console)
                    .ConfigureAwait(false);
                return 0;
            }

            if (command is CommandNames.ImageUnpack or CommandNames.ImagePack) {
                return await ImageCommandRunner.RunAsync(
                        command,
                        options,
                        console)
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(
                        "ARKT_SERVER")) &&
                string.IsNullOrWhiteSpace(
                    options.ServerEndpoint)) {
                await LocalServerManager.EnsureRunningAsync(
                        verboseRequested,
                        console)
                    .ConfigureAwait(false);
                options.ServerEndpoint =
                    ToolkitServerDefaults.Endpoint;
            }

            if (command.StartsWith(
                CommandNames.AdbPrefix,
                StringComparison.Ordinal)) {
                var adbEndpoint =
                    await ToolkitEndpointResolver.ResolveAsync(
                            options,
                            CancellationToken.None)
                        .ConfigureAwait(false) ??
                    throw new InvalidOperationException(
                        "No toolkit server endpoint is available.");
                options.ServerEndpoint = adbEndpoint.HostPort;
                var adbResult = await AdbCommandRunner.ExecuteAsync(
                        command,
                        options,
                        logger,
                        console)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Command {Command} completed in {ElapsedMilliseconds:F1} ms",
                    command,
                    Stopwatch.GetElapsedTime(startedAt)
                        .TotalMilliseconds);
                return adbResult;
            }

            var endpoint =
                await ToolkitEndpointResolver.ResolveAsync(
                        options,
                        CancellationToken.None)
                    .ConfigureAwait(false) ??
                throw new InvalidOperationException(
                    "No toolkit server endpoint is available.");
            options.ServerEndpoint = endpoint.HostPort;
            var result = await ToolkitClientCommandRunner.RunAsync(
                    command,
                    options,
                    logger,
                    console)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Command {Command} completed in {ElapsedMilliseconds:F1} ms",
                command,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return result;
        } catch (Exception error) {
            if (verboseRequested) {
                logger.LogError(error, "Command failed");
            } else {
                logger.LogError("{ErrorMessage}", error.Message);
            }

            return 1;
        }
    }

    private static async Task RunServerAsync(
        CommandOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger,
        string? statePath) {
        var endpoint = ToolkitEndpoint.Parse(
            options.ServerListen
            ?? throw new ArgumentException(
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
            new LocalServerStateStore(statePath);
        if (serialHistoryKiB <= 0) {
            throw new ArgumentException(
                "--serial-history-kib must be greater than zero.");
        }
        Console.WriteLine(
            $"server instance: {instanceId}");
        Console.WriteLine(
            $"server hostname: {hostName}");
        Console.WriteLine(
            $"server endpoint: {endpoint}");
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
                loggerFactory,
                loggerFactory.CreateLogger<ToolkitHost>());
            logger.LogInformation(
                "Toolkit server started at {Endpoint}",
                endpoint);
            await server.RunAsync(stop.Token)
                .ConfigureAwait(false);
        } finally {
            Console.CancelKeyPress -= handler;
        }
    }

    private static bool HasOption(
        IEnumerable<string> arguments,
        params string[] names) {
        foreach (var argument in arguments) {
            if (argument == "--") {
                return false;
            }

            if (names.Contains(argument, StringComparer.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsHelpCommand(string argument) {
        return argument.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            argument.Equals("h", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRootHelpOption(string argument) {
        return argument is "--help" or "-h" or "-?" or "/?";
    }

    private static void WriteMotd(ConsoleRenderer console) {
        if (console.IsInteractive) {
            Motd.Write(Console.Out);
        }
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
