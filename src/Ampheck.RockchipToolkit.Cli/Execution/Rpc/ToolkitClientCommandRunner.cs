using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static partial class ToolkitClientCommandRunner {
    public static async Task<int> RunAsync(
        string command,
        CommandOptions options,
        ILogger logger,
        ConsoleRenderer console) {
        var endpoint = ToolkitEndpoint.Parse(
            options.ServerEndpoint ??
            throw new ArgumentException(
                "A toolkit server endpoint is required."));
        var token = Environment.GetEnvironmentVariable(
            "ARKT_SERVER_TOKEN");
        var clientName = Environment.GetEnvironmentVariable(
            "ARKT_CLIENT_NAME");
        await using var client = new ToolkitRpcClient(
            endpoint,
            token,
            clientName,
            ToolkitFileAccess.Parse(
                Environment.GetEnvironmentVariable(
                    "ARKT_FILE_MODE")));
        await using var payload = new ToolkitPayloadPlan(
            client,
            logger,
            console);
        using var printer = new ToolkitEventPrinter(console);
        void ReportEvent(TaskEventNotification value) {
            printer.Write(value);
        }

        var target = CreateTarget(options);
        return command switch {
            CommandNames.Devices =>
                await RunDevicesAsync(
                        client,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Info =>
                await RunInfoAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.ChipInfo =>
                await RunChipInfoAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.ChipId =>
                await RunChipIdAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Capability =>
                await RunCapabilityAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Loader =>
                await RunLoaderAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Flash =>
                await RunFlashAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Read =>
                await RunReadAsync(
                        client,
                        payload,
                        options,
                        target,
                        logger,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Write =>
                await RunWriteAsync(
                        client,
                        payload,
                        options,
                        target,
                        logger,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Erase =>
                await RunEraseAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Storage =>
                await RunStorageAsync(
                        client,
                        payload,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.Reset =>
                await RunResetAsync(
                        client,
                        options,
                        target,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.SerialList =>
                await RunSerialListAsync(
                        client,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.SerialOpen =>
                await RunSerialOpenAsync(
                        client,
                        options,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.SerialClose =>
                await RunSerialCloseAsync(
                        client,
                        options,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.SerialClear =>
                await RunSerialClearAsync(
                        client,
                        options,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.SerialHistory =>
                await RunSerialHistoryAsync(
                        client,
                        options,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.SerialTerminal =>
                await RunSerialTerminalAsync(
                        client,
                        options,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            _ => throw new ArgumentException(
                $"Command '{command}' is not supported by JSON-RPC.")
        };
    }

    private static async ValueTask<PayloadReference>
        PrepareLoaderAsync(
            ToolkitPayloadPlan payload,
            CommandOptions options) {
        return string.IsNullOrWhiteSpace(options.Loader)
            ? new PayloadReference(
                BlobId: null,
                Url: null,
                ServerPath: null)
            : await payload.PrepareFileAsync(
                    options.Loader,
                    options.FetchLocation,
                    CancellationToken.None)
                .ConfigureAwait(false);
    }

    private static DeviceTarget CreateTarget(
        CommandOptions options) => new() {
            Transport = options.Transport switch {
                TransportKind.RockUsb => ToolkitTransport.RockUsb,
                TransportKind.LibUsb => ToolkitTransport.LibUsb,
                _ => ToolkitTransport.Auto
            },
            VendorId = options.VendorId,
            ProductId = options.ProductId
        };

    private static string RequireSerialPort(
        CommandOptions options) =>
        string.IsNullOrWhiteSpace(options.SerialPort)
            ? throw new ArgumentException(
                "A serial port is required.")
            : options.SerialPort;
}
