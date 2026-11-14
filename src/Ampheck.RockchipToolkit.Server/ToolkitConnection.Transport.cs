using System.Net.WebSockets;
using System.Text;

namespace Ampheck.RockchipToolkit.Server;

/// <summary>Owns serialized WebSocket writes and connection-level error reporting.</summary>
internal sealed partial class ToolkitConnection : IAsyncDisposable {
    private async ValueTask SendStatusAsync(
        ToolkitChannel channel,
        CancellationToken cancellationToken,
        string message) {
        await SendAsync(
                ToolkitWireCodec.Notify(
                    channel.ChannelId,
                    channel.RequestId,
                    channel.NextSequence(),
                    "status",
                    message),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask SendSuccessAsync(
        ToolkitChannel channel,
        CancellationToken cancellationToken,
        string message) {
        await SendAsync(
                ToolkitWireCodec.Notify(
                    channel.ChannelId,
                    channel.RequestId,
                    channel.NextSequence(),
                    "success",
                    message),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask FailChannelAsync(ToolkitChannel channel, string message) {
        _channels.TryRemove(channel.ChannelId, out _);
        await SendAsync(ToolkitWireCodec.ErrorResponse(channel.ChannelId, channel.RequestId,
            ToolkitStatus.InvalidRequest, message), _stop.Token).ConfigureAwait(false);
        await channel.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask SendAsync(ToolkitFrame frame, CancellationToken cancellationToken) {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await ToolkitWebSocketProtocol.SendAsync(_socket, frame, cancellationToken).ConfigureAwait(false);
        } finally {
            _sendGate.Release();
        }
    }

    private async ValueTask SendDownloadAsync(
        ToolkitChannel channel,
        string path,
        CancellationToken cancellationToken) {
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        while (true) {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) {
                break;
            }

            await SendAsync(ToolkitWireCodec.Data(channel.ChannelId, channel.RequestId, buffer.AsMemory(0, read)),
                cancellationToken).ConfigureAwait(false);
        }

        await SendAsync(ToolkitWireCodec.AttachmentEnd(channel.ChannelId, channel.RequestId), cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TrySendAsync(ToolkitFrame frame) {
        try {
            await SendAsync(frame, CancellationToken.None).ConfigureAwait(false);
        } catch (Exception error) when (error is WebSocketException or ObjectDisposedException or IOException) {
        }
    }

    private bool TokenMatches(string? token) {
        if (string.IsNullOrEmpty(_options.Token)) {
            return true;
        }

        if (string.IsNullOrEmpty(token)) {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(_options.Token);
        var actual = Encoding.UTF8.GetBytes(token);
        return expected.Length == actual.Length &&
            System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
