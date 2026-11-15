using System.Net.WebSockets;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Execution.Serial;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal sealed partial class AdbCommandRunner(CliCommandContext context) {
    public async Task<int> ListDevicesAsync(
        AdbDeviceArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = await CreateSessionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var result = await session.Client.ListAdbDevicesAsync(
                new AdbDevicesParams {
                    SerialNumber = options.SerialNumber
                },
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var device in result.Devices) {
            Console.WriteLine(
                $"{device.SerialNumber}\t{device.State}\t" +
                FormatDetails(device));
        }

        Console.Out.Flush();
        return 0;
    }

    public async Task<int> ShellAsync(
        AdbShellArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = await CreateSessionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        if (options.Command is null &&
            options.Arguments.Length == 0) {
            return await RunInteractiveShellAsync(
                    session,
                    options.SerialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var command = options.Command ??
            AdbShellCommandBuilder.FromArguments(
                options.Arguments);
        var result = await session.Client.ExecuteAdbShellAsync(
                new AdbShellParams {
                    SerialNumber = options.SerialNumber,
                    Command = command
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.Out.Write(result.Text);
        if (result.Text.Length > 0 &&
            !result.Text.EndsWith('\n')) {
            Console.WriteLine();
        }

        Console.Out.Flush();
        return 0;
    }

    public async Task<int> RebootAsync(
        AdbRebootArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = await CreateSessionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        if (options.Mode == ToolkitAdbRebootMode.Bootloader) {
            return await RebootToLoaderAsync(
                    session,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var name = options.Mode == ToolkitAdbRebootMode.Recovery
            ? "recovery"
            : "normal";
        context.Console.WriteStatus($"Requesting {name} reboot");
        await session.Client.RebootAdbDeviceAsync(
                new AdbRebootParams {
                    SerialNumber = options.SerialNumber,
                    Mode = options.Mode
                },
                cancellationToken)
            .ConfigureAwait(false);
        context.Console.WriteStatus(
            $"{name} reboot requested",
            complete: true);
        return 0;
    }

    private async ValueTask<CliRpcSession>
        CreateSessionAsync(CancellationToken cancellationToken) {
        var session = context.OpenRpcSession();
        try {
            await session.Client.EnsureAdbServerAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            return session;
        } catch {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<int> RunInteractiveShellAsync(
        CliRpcSession session,
        string? serialNumber,
        CancellationToken cancellationToken) {
        using var socket = await session.Client.OpenAdbShellAsync(
                new AdbShellSessionParams {
                    SerialNumber = serialNumber
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        using var stop = new CancellationTokenSource();
        var output = PumpBinaryOutputAsync(socket, stop.Token);
        try {
            await SerialTerminalRunner.RunAsync(
                    async (data, token) => {
                        await socket.SendAsync(
                                data,
                                WebSocketMessageType.Binary,
                                endOfMessage: true,
                                token)
                            .ConfigureAwait(false);
                    },
                    output,
                    context.Console,
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

    private async Task<int> RebootToLoaderAsync(
        CliRpcSession session,
        AdbRebootArguments options,
        CancellationToken cancellationToken) {
        context.Console.WriteStatus("Requesting Loader mode");
        await session.Client.RebootAdbDeviceAsync(
                new AdbRebootParams {
                    SerialNumber = options.SerialNumber,
                    Mode = ToolkitAdbRebootMode.Bootloader
                },
                cancellationToken)
            .ConfigureAwait(false);
        context.Console.WriteStatus("Waiting for Rockchip Loader");
        var target = new DeviceTarget {
            Transport = options.LoaderTarget.Transport switch {
                TransportKind.RockUsb =>
                    ToolkitTransport.RockUsb,
                TransportKind.LibUsb =>
                    ToolkitTransport.LibUsb,
                _ => ToolkitTransport.Auto
            },
            VendorId = options.LoaderTarget.VendorId,
            ProductId = options.LoaderTarget.ProductId
        };
        for (var attempt = 0; attempt < 40; attempt++) {
            var devices = await session.Client.ListDevicesAsync(
                    new DeviceListParams { Target = target },
                    session.Report,
                    cancellationToken)
                .ConfigureAwait(false);
            if (devices.Devices.Any(device =>
                device.Mode is "Loader" or "MaskRom")) {
                context.Console.WriteStatus(
                    "Device entered Loader mode",
                    complete: true);
                await Task.Delay(
                        500,
                        cancellationToken)
                    .ConfigureAwait(false);
                return 0;
            }

            await Task.Delay(
                    500,
                    cancellationToken)
                .ConfigureAwait(false);
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
