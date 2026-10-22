using System.Diagnostics;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Execution.Serial;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class Application {
    public static async Task<int> RunAsync(string[] args) {
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
                CommandLine.PrintHelp(["--help"]);
                return 0;
            }

            if (args is ["--version"]) {
                WriteMotd(console);
                CommandLine.PrintVersion();
                return 0;
            }

            if (CommandLine.TryPrintCompletion(args)) {
                return 0;
            }

            if (args.Length == 1 &&
                (IsHelpCommand(args[0]) ||
                    IsRootHelpOption(args[0]))) {
                WriteMotd(console);
                CommandLine.PrintHelp(["--help"]);
                return 0;
            }

            if (IsHelpCommand(args[0])) {
                CommandLine.PrintHelp([
                    .. args.Skip(1),
                    "--help"
                ]);
                return 0;
            }

            if (HasOption(args, "--help", "-h", "-?")) {
                CommandLine.PrintHelp(args);
                return 0;
            }

            var invocation = CommandLine.Parse(args);
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
                        logger)
                    .ConfigureAwait(false);
                return 0;
            }

            if (command.StartsWith(
                CommandNames.AdbPrefix,
                StringComparison.Ordinal)) {
                var adbResult = await AdbCommandRunner.ExecuteAsync(
                        command,
                        options,
                        loggerFactory,
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

            if (command.StartsWith(
                CommandNames.SerialPrefix,
                StringComparison.Ordinal)) {
                return await SerialCommandRunner.RunAsync(
                        command,
                        options,
                        loggerFactory,
                        logger,
                        console)
                    .ConfigureAwait(false);
            }

            var directWorker =
                Environment.GetEnvironmentVariable(
                    "ARKT_DIRECT_WORKER") == "1";
            if (!directWorker) {
                var endpoint =
                    await ToolkitEndpointResolver.ResolveAsync(
                            options,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                if (endpoint is not null) {
                    options.ServerEndpoint = endpoint.HostPort;
                    return await ToolkitClientCommandRunner.RunAsync(
                            command,
                            options,
                            logger,
                            console)
                        .ConfigureAwait(false);
                }
            }

            using var operationLock = directWorker
                ? null
                : OperationLock.Acquire();
            var result = await RunInProcessAsync(
                    command,
                    options,
                    verboseRequested,
                    loggerFactory,
                    console,
                    directWorker)
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

    private static async Task<int> RunInProcessAsync(
        string command,
        CommandOptions options,
        bool verbose,
        ILoggerFactory loggerFactory,
        ConsoleRenderer console,
        bool directWorker) {
        var request = ToolkitRequestFactory.Create(
            command,
            options,
            verbose);
        var executor = new ToolkitExecutor(
            loggerFactory,
            ToolkitTransferLimits.FromEnvironment());
        using var printer = directWorker
            ? null
            : new ToolkitEventPrinter(console);
        var result = await executor.ExecuteAsync(
                request,
                async value => {
                    if (directWorker) {
                        await Console.Out.WriteLineAsync(
                                ToolkitWorkerEvent.Encode(value))
                            .ConfigureAwait(false);
                    } else if (printer is not null) {
                        printer.Write(value);
                    } else if (value.Source == "stderr") {
                        await Console.Error.WriteLineAsync(
                                value.Message)
                            .ConfigureAwait(false);
                    } else {
                        await Console.Out.WriteLineAsync(
                                value.Message)
                            .ConfigureAwait(false);
                    }
                },
                CancellationToken.None)
            .ConfigureAwait(false);
        return result.ExitCode;
    }

    private static async Task RunServerAsync(
        CommandOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger) {
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
                        : TimeSpan.FromSeconds(idleSeconds)
                },
                new ToolkitProcessExecutor(limits),
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
            MaximumAttachmentBytes = ToBytes(
                options.ServerMaxAttachmentMiB ?? 4096),
            MaximumRequestBytes = ToBytes(
                options.ServerMaxRequestMiB ?? 8192)
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
