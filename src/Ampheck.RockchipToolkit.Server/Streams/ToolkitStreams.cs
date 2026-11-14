using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Ampheck.Adb;
using Ampheck.RockchipToolkit.Server.Serial;
using Ampheck.RockchipToolkit.Transport.Serial;

namespace Ampheck.RockchipToolkit.Server.Streams;

internal static class ToolkitStreams {
    public static async Task RunSerialAsync(
        WebSocket socket,
        SerialTerminalLease lease,
        CancellationToken serverToken) {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            serverToken);
        try {
            await using (lease) {
                if (lease.History.Length > 0) {
                    await socket.SendAsync(
                            lease.History,
                            WebSocketMessageType.Binary,
                            endOfMessage: true,
                            stop.Token)
                        .ConfigureAwait(false);
                }

                var output = SendSerialOutputAsync(
                    socket,
                    lease.Session,
                    stop.Token);
                var input = ReceiveSerialInputAsync(
                    socket,
                    lease.Session,
                    stop.Token);
                var completed = await Task.WhenAny(input, output)
                    .ConfigureAwait(false);
                await stop.CancelAsync().ConfigureAwait(false);
                if (completed == input) {
                    await input.ConfigureAwait(false);
                    await IgnoreCancellationAsync(output)
                        .ConfigureAwait(false);
                } else {
                    await output.ConfigureAwait(false);
                    await IgnoreCancellationAsync(input)
                        .ConfigureAwait(false);
                }
            }
        } finally {
            await CloseAsync(socket).ConfigureAwait(false);
        }
    }

    public static async Task RunAdbShellAsync(
        WebSocket socket,
        AdbShell shell,
        CancellationToken serverToken) {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            serverToken);
        await using (shell) {
            var output = Channel.CreateUnbounded<string>(
                new UnboundedChannelOptions {
                    SingleReader = true,
                    SingleWriter = true
                });
            var read = shell.ReadAsync(
                value => output.Writer.TryWrite(value),
                stop.Token);
            var send = SendAdbOutputAsync(
                socket,
                output.Reader,
                stop.Token);
            var input = ReceiveAdbInputAsync(
                socket,
                shell,
                stop.Token);
            var completed = await Task.WhenAny(
                    read,
                    send,
                    input)
                .ConfigureAwait(false);
            await stop.CancelAsync().ConfigureAwait(false);
            await IgnoreCancellationAsync(read)
                .ConfigureAwait(false);
            await IgnoreCancellationAsync(send)
                .ConfigureAwait(false);
            await IgnoreCancellationAsync(input)
                .ConfigureAwait(false);
            if (completed == send) {
                output.Writer.TryComplete();
            }
        }

        await CloseAsync(socket).ConfigureAwait(false);
    }

    private static async Task SendSerialOutputAsync(
        WebSocket socket,
        SerialSession session,
        CancellationToken cancellationToken) {
        await foreach (var value in session.Events.ReadAllAsync(
            cancellationToken)) {
            if (value.Kind != SerialEventKind.Data) {
                continue;
            }

            await socket.SendAsync(
                    value.Data,
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task ReceiveSerialInputAsync(
        WebSocket socket,
        SerialSession session,
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

            await session.WriteAsync(
                    buffer.AsMemory(0, result.Count),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task SendAdbOutputAsync(
        WebSocket socket,
        ChannelReader<string> output,
        CancellationToken cancellationToken) {
        await foreach (var value in output.ReadAllAsync(
            cancellationToken)) {
            await socket.SendAsync(
                    Encoding.UTF8.GetBytes(value),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task ReceiveAdbInputAsync(
        WebSocket socket,
        AdbShell shell,
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

            await shell.WriteAsync(
                    buffer.AsMemory(0, result.Count),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task IgnoreCancellationAsync(Task task) {
        try {
            await task.ConfigureAwait(false);
        } catch (OperationCanceledException) {
        }
    }

    private static async Task CloseAsync(WebSocket socket) {
        if (socket.State is not (
            WebSocketState.Open or
            WebSocketState.CloseReceived)) {
            return;
        }

        try {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(2));
            await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    null,
                    timeout.Token)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is WebSocketException or
            OperationCanceledException or
            ObjectDisposedException) {
        }
    }
}
