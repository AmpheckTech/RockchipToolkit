using System.Net.WebSockets;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Serial;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static partial class ToolkitClientCommandRunner {
    private static async Task<int> RunSerialListAsync(
        ToolkitRpcClient client,
        Action<TaskEventNotification> report) {
        var result = await client.RequestAsync<
            EmptyParams,
            SerialListResult>(
                JsonRpcMethods.SerialList,
                new EmptyParams(),
                report)
            .ConfigureAwait(false);
        foreach (var port in result.Ports) {
            Console.WriteLine(port);
        }

        return 0;
    }

    private static async Task<int> RunSerialOpenAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        Action<TaskEventNotification> report) {
        await client.RequestAsync<
            SerialOpenParams,
            EmptyResult>(
                JsonRpcMethods.SerialOpen,
                new SerialOpenParams {
                    PortName = RequireSerialPort(options),
                    Settings = new SerialPortSettings {
                        BaudRate =
                            options.SerialBaudRate ?? 150_000,
                        DataBits = options.SerialDataBits ?? 8,
                        Parity = options.SerialParity,
                        StopBits = options.SerialStopBits,
                        Handshake = options.SerialHandshake,
                        DtrEnable = options.SerialDtrEnable,
                        RtsEnable = options.SerialRtsEnable
                    }
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"Serial port {options.SerialPort} opened");
        return 0;
    }

    private static async Task<int> RunSerialCloseAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        Action<TaskEventNotification> report) {
        var result = await client.RequestAsync<
            SerialCloseParams,
            SerialCloseResult>(
                JsonRpcMethods.SerialClose,
                new SerialCloseParams {
                    PortName = RequireSerialPort(options)
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(
            result.Closed
                ? $"Serial port {options.SerialPort} closed"
                : $"Serial port {options.SerialPort} was not open");
        return 0;
    }

    private static async Task<int> RunSerialClearAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        Action<TaskEventNotification> report) {
        await client.RequestAsync<
            SerialPortParams,
            SerialClearResult>(
                JsonRpcMethods.SerialClear,
                new SerialPortParams {
                    PortName = RequireSerialPort(options)
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"Serial buffer cleared for {options.SerialPort}");
        return 0;
    }

    private static async Task<int> RunSerialHistoryAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        Action<TaskEventNotification> report) {
        var file = await client.RequestAsync<
            SerialHistoryParams,
            FileReference>(
                JsonRpcMethods.SerialHistory,
                new SerialHistoryParams {
                    PortName = RequireSerialPort(options),
                    MaximumBytes = options.SerialHistoryBytes ??
                        256 * 1024,
                    OutputServerPath = client.UseServerPaths
                        ? Path.Combine(
                            Path.GetTempPath(),
                            $"arkt-serial-history-{Guid.NewGuid():N}.bin")
                        : null
                },
                report)
            .ConfigureAwait(false);
        try {
            await BlobOutput.CopyToStandardOutputAsync(
                    client,
                    file)
                .ConfigureAwait(false);
        } finally {
            if (file.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId)
                    .ConfigureAwait(false);
            } else if (file.Path is { } path) {
                TryDelete(path);
            }
        }

        return 0;
    }

    private static async Task<int> RunSerialTerminalAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        using var socket = await client.OpenStreamAsync<
            SerialTerminalParams>(
                JsonRpcMethods.SerialTerminal,
                new SerialTerminalParams {
                    PortName = RequireSerialPort(options),
                    HistoryBytes = options.SerialHistoryBytes ??
                        256 * 1024
                },
                "/api/serial",
                "arkt.serial.v1",
                report)
            .ConfigureAwait(false);
        using var stop = new CancellationTokenSource();
        var output = PumpBinaryOutputAsync(
            socket,
            stop.Token);
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
                    stop.Token)
                .ConfigureAwait(false);
        } finally {
            await stop.CancelAsync().ConfigureAwait(false);
            TryCloseSocket(socket);
            await IgnoreCancellationAsync(output)
                .ConfigureAwait(false);
        }

        return 0;
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

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
