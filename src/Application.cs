using System.Diagnostics;
using Ampheck.RockchipToolkit.Arkt.CommandLine;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Runtime;
using Ampheck.RockchipToolkit.Runtime.Logging;
using Ampheck.RockchipToolkit.Runtime.Terminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit;

internal static class Application {
    public static async Task<int> RunAsync(string[] args) {
        var verboseRequested = CommandLineInput.HasOption(
            args,
            "--verbose",
            "-v");
        using var console = new ConsoleRenderer();

        try {
            var commandLine = new CommandLine(Console.Out);
            if (args.Length == 0) {
                CommandLineInput.WriteMotd(console);
                commandLine.PrintHelp(["--help"]);
                return 0;
            }

            if (args is ["--version"]) {
                CommandLineInput.WriteMotd(console);
                commandLine.PrintVersion();
                return 0;
            }

            if (commandLine.TryPrintCompletion(args)) {
                return 0;
            }

            if (args.Length == 1 &&
                (CommandLineInput.IsHelpCommand(args[0]) ||
                    CommandLineInput.IsRootHelpOption(args[0]))) {
                CommandLineInput.WriteMotd(console);
                commandLine.PrintHelp(["--help"]);
                return 0;
            }

            if (CommandLineInput.IsHelpCommand(args[0])) {
                commandLine.PrintHelp([.. args.Skip(1), "--help"]);
                return 0;
            }

            if (CommandLineInput.HasOption(args, "--help", "-h", "-?")) {
                commandLine.PrintHelp(args);
                return 0;
            }

            var invocation = commandLine.Parse(args);
            if (invocation is null) {
                return 0;
            }

            using var loggerFactory = LoggingFactory.Create(
                verboseRequested,
                informational: false,
                console);
            var logger = loggerFactory.CreateLogger(
                "Ampheck.RockchipToolkit");
            var startedAt = Stopwatch.GetTimestamp();
            await using var services = ServiceComposition.Create(
                verboseRequested,
                args,
                console,
                loggerFactory);
            await using var scope = services.CreateAsyncScope();
            var command = ActivatorUtilities.CreateInstance(
                scope.ServiceProvider,
                invocation.Command.Type);
            var result = await invocation.Binder.InvokeAsync(
                    command,
                    invocation.ParseResult,
                    scope.ServiceProvider,
                    CancellationToken.None)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Command {Command} completed in {ElapsedMilliseconds:F1} ms",
                invocation.Command.CommandName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return result;
        } catch (Exception error) {
            using var loggerFactory = LoggingFactory.Create(
                verboseRequested,
                informational: false,
                console);
            var logger = loggerFactory.CreateLogger(
                "Ampheck.RockchipToolkit");
            if (verboseRequested) {
                logger.LogError(error, "Command failed");
            } else {
                logger.LogError("{ErrorMessage}", error.Message);
            }

            return 1;
        }
    }
}
