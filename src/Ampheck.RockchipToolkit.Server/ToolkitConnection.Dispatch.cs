using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

/// <summary>Handles frame-level dispatch and channel attachment lifecycle.</summary>
internal sealed partial class ToolkitConnection : IAsyncDisposable {
    private async ValueTask DispatchAsync(ToolkitFrame frame) {
        if (!ToolkitWireCodec.IsValidFrame(frame)) {
            await SendAsync(ToolkitWireCodec.ErrorResponse(frame.ChannelId, frame.RequestId,
                    ToolkitStatus.UnsupportedProtocol, "Unsupported toolkit protocol version."),
                _stop.Token)
                .ConfigureAwait(false);
            return;
        }

        switch (frame.Kind) {
            case ToolkitFrameKind.Request when frame.Opcode == ToolkitOpcode.Call:
                await StartChannelAsync(frame).ConfigureAwait(false);
                break;
            case ToolkitFrameKind.Data:
                await ReceiveAttachmentDataAsync(frame).ConfigureAwait(false);
                break;
            case ToolkitFrameKind.AttachmentEnd:
                await CompleteAttachmentAsync(frame).ConfigureAwait(false);
                break;
            case ToolkitFrameKind.Close:
                if (_channels.TryGetValue(frame.ChannelId, out var channel)) {
                    channel.RequestClose();
                }

                break;
            case ToolkitFrameKind.Cancel:
                if (_channels.TryGetValue(frame.ChannelId, out var cancelled)) {
                    cancelled.Cancel();
                }

                break;
            default:
                await SendAsync(ToolkitWireCodec.ErrorResponse(frame.ChannelId, frame.RequestId,
                        ToolkitStatus.InvalidRequest, "Unsupported toolkit frame."),
                    _stop.Token)
                    .ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask StartChannelAsync(ToolkitFrame frame) {
        if (frame.ChannelId == 0 || _channels.ContainsKey(frame.ChannelId)) {
            await SendAsync(ToolkitWireCodec.ErrorResponse(frame.ChannelId, frame.RequestId,
                    ToolkitStatus.InvalidRequest, "The toolkit channel ID is invalid."),
                _stop.Token)
                .ConfigureAwait(false);
            return;
        }

        if (_channels.Count >= MaximumChannels) {
            await SendAsync(ToolkitWireCodec.ErrorResponse(
                    frame.ChannelId,
                    frame.RequestId,
                    ToolkitStatus.Busy,
                    "The connection has too many active channels."),
                _stop.Token)
                .ConfigureAwait(false);
            return;
        }

        ToolkitRequest request;
        try {
            request = ToolkitRequestCodec.Decode(frame.Payload);
            ToolkitRequestValidator.Validate(
                request,
                _options.TransferLimits);
        } catch (UnsupportedToolkitMethodException error) {
            await SendAsync(ToolkitWireCodec.ErrorResponse(frame.ChannelId, frame.RequestId,
                    ToolkitStatus.UnsupportedMethod, error.Message),
                _stop.Token)
                .ConfigureAwait(false);
            return;
        } catch (InvalidDataException error) {
            await SendAsync(ToolkitWireCodec.ErrorResponse(frame.ChannelId, frame.RequestId,
                    ToolkitStatus.InvalidRequest, error.Message),
                _stop.Token)
                .ConfigureAwait(false);
            return;
        }

        var channel = new ToolkitChannel(
            frame.ChannelId,
            frame.RequestId,
            request,
            _options.TransferLimits,
            _logger);
        _channels[frame.ChannelId] = channel;
        _logger.LogInformation(
            "Operation requested: client={ClientName} " +
            "connection={ConnectionId} channel={ChannelId} " +
            "operation={Operation} target={Target}",
            _clientName,
            _connectionId,
            frame.ChannelId,
            ToolkitOperationNames.GetName(request.Operation),
            DescribeTarget(request));
        if (channel.IsReady) {
            _ = ExecuteChannelAsync(channel).ConfigureAwait(false);
        }
    }

    private async ValueTask ReceiveAttachmentDataAsync(ToolkitFrame frame) {
        if (!_channels.TryGetValue(frame.ChannelId, out var channel)) {
            return;
        }

        try {
            await channel.AppendAsync(frame.Payload, _stop.Token).ConfigureAwait(false);
        } catch (Exception error) when (error is InvalidDataException or IOException) {
            await FailChannelAsync(channel, error.Message).ConfigureAwait(false);
        }
    }

    private async ValueTask CompleteAttachmentAsync(ToolkitFrame frame) {
        if (!_channels.TryGetValue(frame.ChannelId, out var channel)) {
            return;
        }

        try {
            if (await channel.CompleteAttachmentAsync(_stop.Token).ConfigureAwait(false)) {
                _ = ExecuteChannelAsync(channel).ConfigureAwait(false);
            }
        } catch (Exception error) when (error is InvalidDataException or IOException) {
            await FailChannelAsync(channel, error.Message).ConfigureAwait(false);
        }
    }
}
