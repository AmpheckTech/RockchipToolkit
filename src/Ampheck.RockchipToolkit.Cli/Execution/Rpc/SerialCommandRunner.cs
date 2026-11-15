using System.Net.WebSockets;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Serial;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class SerialCommandRunner(CliCommandContext context) {
    public async Task<int> RunListAsync(
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var result = await session.Client.ListSerialPortsAsync(
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var port in result.Ports) {
            Console.WriteLine(port);
        }

        return 0;
    }

    public async Task<int> RunOpenAsync(
        SerialOpenArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        await session.Client.OpenSerialPortAsync(
                new SerialOpenParams {
                    PortName = options.PortName,
                    Settings = options.Settings
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"Serial port {options.PortName} opened");
        return 0;
    }

    public async Task<int> RunCloseAsync(
        SerialPortArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var result = await session.Client.CloseSerialPortAsync(
                new SerialCloseParams {
                    PortName = options.PortName
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            result.Closed
                ? $"Serial port {options.PortName} closed"
                : $"Serial port {options.PortName} was not open");
        return 0;
    }

    public async Task<int> RunClearAsync(
        SerialPortArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        await session.Client.ClearSerialBufferAsync(
                new SerialPortParams {
                    PortName = options.PortName
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"Serial buffer cleared for {options.PortName}");
        return 0;
    }

    public async Task<int> RunHistoryAsync(
        SerialHistoryArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var file = await session.Client.ReadSerialHistoryAsync(
                new SerialHistoryParams {
                    PortName = options.PortName,
                    MaximumBytes = options.MaximumBytes,
                    OutputServerPath = session.Client.UseServerPaths
                        ? Path.Combine(
                            Path.GetTempPath(),
                            $"arkt-serial-history-{Guid.NewGuid():N}.bin")
                        : null
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        try {
            await BlobOutput.CopyToStandardOutputAsync(
                    session.Client,
                    file,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            if (file.BlobId is { } blobId) {
                await session.Client.DeleteBlobAsync(blobId)
                    .ConfigureAwait(false);
            } else if (file.Path is { } path) {
                TryDelete(path);
            }
        }

        return 0;
    }

    public async Task<int> RunTerminalAsync(
        SerialTerminalArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        using var socket = await session.Client.OpenSerialTerminalAsync(
                new SerialTerminalParams {
                    PortName = options.PortName,
                    HistoryBytes = options.HistoryBytes
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        using var stop = new CancellationTokenSource();
        var output = PumpBinaryOutputAsync(
            socket,
            stop.Token);
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
