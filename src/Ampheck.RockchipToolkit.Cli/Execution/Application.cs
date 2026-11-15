using System.Diagnostics;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
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
        var commandLine = new CommandLine(Console.Out);

        try {
            if (args.Length == 0) {
                WriteMotd(console);
                commandLine.PrintHelp(["--help"]);
                return 0;
            }

            if (args is ["--version"]) {
                WriteMotd(console);
                commandLine.PrintVersion();
                return 0;
            }

            if (commandLine.TryPrintCompletion(args)) {
                return 0;
            }

            if (args.Length == 1 &&
                (IsHelpCommand(args[0]) ||
                    IsRootHelpOption(args[0]))) {
                WriteMotd(console);
                commandLine.PrintHelp(["--help"]);
                return 0;
            }

            if (IsHelpCommand(args[0])) {
                commandLine.PrintHelp([.. args.Skip(1), "--help"]);
                return 0;
            }

            if (HasOption(args, "--help", "-h", "-?")) {
                commandLine.PrintHelp(args);
                return 0;
            }

            var invocation = commandLine.Parse(args);
            if (invocation is null) {
                return 0;
            }

            logger.LogDebug(
                "Starting command {Command}",
                invocation.Command);
            var startedAt = Stopwatch.GetTimestamp();
            ToolkitEndpoint? endpoint = null;
            if (invocation.RequiresServer) {
                endpoint = await EnsureServerAsync(
                        verboseRequested,
                        console)
                    .ConfigureAwait(false);
            }

            var context = new CliCommandContext(
                verboseRequested,
                daemon.StatePath,
                loggerFactory,
                logger,
                console,
                endpoint,
                CancellationToken.None);
            var result = await invocation.Execute(
                    context,
                    CancellationToken.None)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Command {Command} completed in {ElapsedMilliseconds:F1} ms",
                invocation.Command,
                Stopwatch.GetElapsedTime(startedAt)
                    .TotalMilliseconds);
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

    private static async Task<ToolkitEndpoint> EnsureServerAsync(
        bool verbose,
        ConsoleRenderer console) {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    "ARKT_SERVER"))) {
            await LocalServerManager.EnsureRunningAsync(
                    verbose,
                    console)
                .ConfigureAwait(false);
        }

        var endpoint =
            await ToolkitEndpointResolver.ResolveAsync(
                    requestedEndpoint: null,
                    CancellationToken.None)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                "No toolkit server endpoint is available.");
        return endpoint;
    }

    private static bool HasOption(
        IEnumerable<string> arguments,
        params string[] names) {
        foreach (var argument in arguments) {
            if (argument == "--") {
                return false;
            }

            if (names.Contains(
                    argument,
                    StringComparer.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsHelpCommand(string argument) {
        return argument.Equals(
                "help",
                StringComparison.OrdinalIgnoreCase) ||
            argument.Equals(
                "h",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRootHelpOption(string argument) {
        return argument is "--help" or "-h" or "-?" or "/?";
    }

    private static void WriteMotd(ConsoleRenderer console) {
        if (console.IsInteractive) {
            Motd.Write(Console.Out);
        }
    }
}
