using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Serial;

internal static class SerialCommandRunner {
    public static async Task<int> RunAsync(
        string command,
        CommandOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger,
        ConsoleRenderer console) {
        var endpoint = await ToolkitEndpointResolver.ResolveAsync(
                options,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (endpoint is not null) {
            options.ServerEndpoint = endpoint.HostPort;
            return await RunRemoteAsync(
                    command,
                    options,
                    logger,
                    console)
                .ConfigureAwait(false);
        }

        return await RunLocalAsync(
                command,
                options,
                loggerFactory,
                console)
            .ConfigureAwait(false);
    }

    private static async Task<int> RunRemoteAsync(
        string command,
        CommandOptions options,
        ILogger logger,
        ConsoleRenderer console) {
        var endpoint = ToolkitEndpoint.Parse(
            options.ServerEndpoint!);
        var token = Environment.GetEnvironmentVariable(
            "ARKT_SERVER_TOKEN");
        await using var client = new ToolkitClient(
            endpoint,
            token);
        var request = ToolkitRequestFactory.Create(
            command,
            options,
            verbose: false);
        using var printer = new ToolkitEventPrinter(console);
        if (command == CommandNames.SerialTerminal) {
            await using var session =
                await client.OpenTerminalAsync(
                        request,
                        printer.Write,
                        data => {
                            Console.OpenStandardOutput()
                                .Write(data.Span);
                        })
                    .ConfigureAwait(false);
            await SerialTerminalRunner.RunAsync(
                    (data, cancellationToken) =>
                        session.WriteAsync(
                            data,
                            cancellationToken),
                    session.Completion,
                    console)
                .ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
            var terminalResult = await session.Completion
                .ConfigureAwait(false);
            if (!terminalResult.Success &&
                !string.IsNullOrWhiteSpace(
                    terminalResult.Error)) {
                logger.LogError(
                    "{ErrorMessage}",
                    terminalResult.Error);
            }

            return terminalResult.ExitCode;
        }

        var result = await client.ExecuteAsync(
                request,
                printer.Write)
            .ConfigureAwait(false);
        if (result.Success &&
            command == CommandNames.SerialHistory &&
            result.Output is { Length: > 0 } history) {
            await SerialTerminalRunner.WriteOutputAsync(
                    history)
                .ConfigureAwait(false);
        }

        if (!result.Success &&
            !string.IsNullOrWhiteSpace(result.Error)) {
            logger.LogError(
                "{ErrorMessage}",
                result.Error);
        }

        return result.ExitCode;
    }

    private static async Task<int> RunLocalAsync(
        string command,
        CommandOptions options,
        ILoggerFactory loggerFactory,
        ConsoleRenderer console) {
        await using var serial =
            new ToolkitSerialService(loggerFactory);
        var request = ToolkitRequestFactory.Create(
            command,
            options,
            verbose: false);
        switch (command) {
            case CommandNames.SerialList:
                foreach (var device in serial.ListDevices()) {
                    Console.WriteLine(device.PortName);
                }

                return 0;
            case CommandNames.SerialHistory:
                await SerialTerminalRunner.WriteOutputAsync(
                        serial.GetHistory(request))
                    .ConfigureAwait(false);
                return 0;
            case CommandNames.SerialOpen:
            case CommandNames.SerialClose:
                throw new InvalidOperationException(
                    "serial open and serial close require a running " +
                    "server; set ARKT_SERVER or start " +
                    $"{ToolkitServerDefaults.Endpoint}.");
            case CommandNames.SerialTerminal: {
                    await using var session =
                        await serial.AttachAsync(request)
                            .ConfigureAwait(false);
                    var output = PumpSessionAsync(
                        session,
                        console);
                    await SerialTerminalRunner.RunAsync(
                            (data, cancellationToken) =>
                                session.WriteAsync(
                                    data,
                                    cancellationToken),
                            output,
                            console)
                        .ConfigureAwait(false);
                    return 0;
                }
            default:
                throw new ArgumentException(
                    $"Unknown serial command '{command}'.");
        }
    }

    private static async Task PumpSessionAsync(
        Ampheck.Serial.SerialSession session,
        ConsoleRenderer console) {
        await foreach (var value in session.Events.ReadAllAsync()) {
            if (value.Kind ==
                Ampheck.Serial.SerialEventKind.Data) {
                await SerialTerminalRunner.WriteOutputAsync(
                        value.Data)
                    .ConfigureAwait(false);
            } else if (value.Message is not null) {
                console.WriteStatus(value.Message);
            }
        }
    }
}
