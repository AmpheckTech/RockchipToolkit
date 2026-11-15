using System.Net.WebSockets;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Execution.Serial;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static partial class AdbCommandRunner {
    public static async Task<int> ExecuteAsync(
        string command,
        CommandOptions options,
        ToolkitEndpoint endpoint,
        ILogger logger,
        ConsoleRenderer console) {
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
        using var printer = new ToolkitEventPrinter(console);
        void ReportEvent(TaskEventNotification value) {
            printer.Write(value);
        }

        await client.EnsureAdbServerAsync()
            .ConfigureAwait(false);
        return command switch {
            CommandNames.AdbDevices =>
                await ListDevicesAsync(
                        client,
                        options,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.AdbShell =>
                await RunShellAsync(
                        client,
                        options,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.AdbPush =>
                await PushAsync(
                        client,
                        options,
                        console,
                        logger,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.AdbPull =>
                await PullAsync(
                        client,
                        options,
                        console,
                        logger,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.AdbRebootSystem =>
                await RebootAsync(
                        client,
                        options,
                        ToolkitAdbRebootMode.System,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.AdbRebootRecovery =>
                await RebootAsync(
                        client,
                        options,
                        ToolkitAdbRebootMode.Recovery,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            CommandNames.AdbRebootLoader =>
                await RebootToLoaderAsync(
                        client,
                        options,
                        console,
                        ReportEvent)
                    .ConfigureAwait(false),
            _ => throw new ArgumentException(
                $"Unknown ADB command '{command}'.")
        };
    }

    private static async Task<int> ListDevicesAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        Action<TaskEventNotification> report) {
        var result = await client.ListAdbDevicesAsync(
                new AdbDevicesParams {
                    SerialNumber = options.AdbSerial
                })
            .ConfigureAwait(false);
        foreach (var device in result.Devices) {
            Console.WriteLine(
                $"{device.SerialNumber}\t{device.State}\t" +
                FormatDetails(device));
        }

        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> RunShellAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        if (options.AdbShellCommand is null &&
            options.AdbShellArguments.Length == 0) {
            return await RunInteractiveShellAsync(
                    client,
                    options,
                    console,
                    report)
                .ConfigureAwait(false);
        }

        var command = options.AdbShellCommand ??
            AdbShellCommandBuilder.FromArguments(
                options.AdbShellArguments);
        var result = await client.ExecuteAdbShellAsync(
                new AdbShellParams {
                    SerialNumber = options.AdbSerial,
                    Command = command
                },
                report)
            .ConfigureAwait(false);
        Console.Out.Write(result.Text);
        if (result.Text.Length > 0 &&
            !result.Text.EndsWith('\n')) {
            Console.WriteLine();
        }

        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> RunInteractiveShellAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        using var socket = await client.OpenAdbShellAsync(
                new AdbShellSessionParams {
                    SerialNumber = options.AdbSerial
                },
                report)
            .ConfigureAwait(false);
        using var stop = new CancellationTokenSource();
        var output = PumpBinaryOutputAsync(socket, stop.Token);
        try {
            await SerialTerminalRunner.RunAsync(
                    async (data, cancellationToken) => {
                        await socket.SendAsync(
                                data,
                                WebSocketMessageType.Binary,
                                endOfMessage: true,
                                cancellationToken)
                            .ConfigureAwait(false);
                    },
                    output,
                    console,
                    stop.Token,
                    "Interactive ADB shell. Use Ctrl+] to leave.")
                .ConfigureAwait(false);
        } finally {
            await stop.CancelAsync().ConfigureAwait(false);
            TryCloseSocket(socket);
            await IgnoreCancellationAsync(output)
                .ConfigureAwait(false);
        }

        return 0;
    }

    private static async Task<int> RebootAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ToolkitAdbRebootMode mode,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        var name = mode == ToolkitAdbRebootMode.Recovery
            ? "recovery"
            : "normal";
        console.WriteStatus($"Requesting {name} reboot");
        await client.RebootAdbDeviceAsync(
                new AdbRebootParams {
                    SerialNumber = options.AdbSerial,
                    Mode = mode
                })
            .ConfigureAwait(false);
        console.WriteStatus(
            $"{name} reboot requested",
            complete: true);
        return 0;
    }

    private static async Task<int> RebootToLoaderAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        console.WriteStatus("Requesting Loader mode");
        await client.RebootAdbDeviceAsync(
                new AdbRebootParams {
                    SerialNumber = options.AdbSerial,
                    Mode = ToolkitAdbRebootMode.Bootloader
                })
            .ConfigureAwait(false);
        console.WriteStatus("Waiting for Rockchip Loader");
        var target = new DeviceTarget {
            Transport = options.Transport switch {
                TransportKind.RockUsb =>
                    ToolkitTransport.RockUsb,
                TransportKind.LibUsb =>
                    ToolkitTransport.LibUsb,
                _ => ToolkitTransport.Auto
            },
            VendorId = options.VendorId,
            ProductId = options.ProductId
        };
        for (var attempt = 0; attempt < 40; attempt++) {
            var devices = await client.ListDevicesAsync(
                    new DeviceListParams { Target = target },
                    report)
                .ConfigureAwait(false);
            if (devices.Devices.Any(device =>
                device.Mode is "Loader" or "MaskRom")) {
                console.WriteStatus(
                    "Device entered Loader mode",
                    complete: true);
                await Task.Delay(500).ConfigureAwait(false);
                return 0;
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The device did not enter Loader mode within 20 seconds.");
    }

    private static async Task PumpBinaryOutputAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken) {
        var buffer = new byte[16 * 1024];
        while (true) {
            var result = await socket.ReceiveAsync(
                    buffer,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) {
                return;
            }

            await SerialTerminalRunner.WriteOutputAsync(
                    buffer.AsMemory(0, result.Count),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string FormatDetails(
        AdbDeviceDescriptor device) {
        var details = new[] {
            FormatField("product", device.Product),
            FormatField("model", device.Model),
            FormatField("device", device.DeviceName)
        }.Where(value => value is not null);
        var text = string.Join(" ", details);
        return text.Length == 0 ? "adb server" : text;
    }

    private static string? FormatField(
        string name,
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : $"{name}:{value}";

    private static string RequireTransferPath(
        string? value,
        string description) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                $"An ADB {description} is required.")
            : value;

    private static void TryCloseSocket(ClientWebSocket socket) {
        try {
            if (socket.State is
                WebSocketState.Open or
                WebSocketState.CloseReceived) {
                socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        null,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
        } catch (Exception error) when (
            error is WebSocketException or
            ObjectDisposedException) {
        }
    }

    private static async Task IgnoreCancellationAsync(Task task) {
        try {
            await task.ConfigureAwait(false);
        } catch (OperationCanceledException) {
        } catch (WebSocketException) {
        }
    }
}
