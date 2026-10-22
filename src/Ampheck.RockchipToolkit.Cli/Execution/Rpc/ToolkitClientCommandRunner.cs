using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server.IO;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitClientCommandRunner {
    public static async Task<int> RunAsync(string command, CommandOptions options, ILogger logger, ConsoleRenderer console) {
        var endpoint = ToolkitEndpoint.Parse(options.ServerEndpoint ??
            throw new ArgumentException("A toolkit server endpoint is required."));
        var token = Environment.GetEnvironmentVariable(
            "ARKT_SERVER_TOKEN");
        await using var client = new ToolkitClient(endpoint, token);
        var request = ToolkitRequestFactory.Create(command, options, HasVerboseLogging());
        await using var attachments =
            await ToolkitAttachmentPlan.CreateAsync(
                    request,
                    options,
                    logger,
                    console,
                    CancellationToken.None)
            .ConfigureAwait(false);
        using var printer = new ToolkitEventPrinter(console);
        var failureReported = false;
        void ReportEvent(ToolkitEvent value) {
            if (value.Source == "stderr" &&
                (value.Message.StartsWith("fail ", StringComparison.OrdinalIgnoreCase) ||
                    value.Message.Contains(" fail ", StringComparison.OrdinalIgnoreCase) ||
                    value.Message.StartsWith("error:", StringComparison.OrdinalIgnoreCase))) {
                failureReported = true;
            }

            printer.Write(value);
        }

        var result = await client.ExecuteAsync(
                attachments.Request,
                ReportEvent,
                attachments.OpenAsync,
                CancellationToken.None,
                ReportEvent)
            .ConfigureAwait(false);
        if (result.Success && result.Output is { Length: > 0 } output) {
            var destination = command switch {
                CommandNames.Read => options.File,
                CommandNames.Write => options.Backup,
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(destination)) {
                await ToolkitOutput.Create(destination)
                    .WriteAsync(output, logger)
                    .ConfigureAwait(false);
            }
        }

        if (!result.Success &&
            !failureReported &&
            !string.IsNullOrWhiteSpace(result.Error)) {
            Console.Error.WriteLine($"error: {result.Error}");
        }

        return result.ExitCode;
    }

    private static bool HasVerboseLogging() {
        return Environment.GetCommandLineArgs().Any(argument => argument is "--verbose" or "-v");
    }
}
