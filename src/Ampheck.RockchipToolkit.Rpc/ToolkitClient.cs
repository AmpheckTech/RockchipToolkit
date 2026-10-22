using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace Ampheck.RockchipToolkit.Rpc;

public sealed class ToolkitClient(ToolkitEndpoint endpoint, string? token = null) : IAsyncDisposable {
    private readonly ToolkitEndpoint _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    private readonly string? _token = token;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<uint, ToolkitPendingCall> _calls = [];
    private readonly CancellationTokenSource _stop = new();
    private ClientWebSocket? _socket;
    private Task? _receiveTask;
    private uint _nextChannelId;
    private uint _nextRequestId;
    private bool _disposed;

    public ToolkitHello? Hello { get; private set; }

    public async Task<ToolkitResult> ExecuteAsync(ToolkitRequest request, Action<ToolkitEvent>? onEvent,
        ToolkitAttachmentOpener? openAttachment = null,
        CancellationToken cancellationToken = default,
        Action<ToolkitEvent>? onTransferEvent = null) {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var channelId = Interlocked.Increment(ref _nextChannelId);
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var pending = new ToolkitPendingCall(onEvent);
        _calls[channelId] = pending;
        try {
            await SendAsync(ToolkitWireCodec.CreateFrame(
                    ToolkitFrameKind.Request, ToolkitOpcode.Call, channelId, requestId,
                    ToolkitRequestCodec.Encode(request)),
                cancellationToken).ConfigureAwait(false);
            await SendAttachmentsAsync(
                    channelId,
                    requestId,
                    request.Attachments,
                    openAttachment,
                    onTransferEvent,
                    cancellationToken)
                .ConfigureAwait(false);
            return await pending.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            await TrySendAsync(ToolkitWireCodec.Cancel(channelId, requestId)).ConfigureAwait(false);
            throw;
        } finally {
            _calls.TryRemove(channelId, out _);
        }
    }

    public async ValueTask<ToolkitTerminalSession> OpenTerminalAsync(
        ToolkitRequest request,
        Action<ToolkitEvent>? onEvent,
        Action<ReadOnlyMemory<byte>> onData,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onData);
        await EnsureConnectedAsync(cancellationToken)
            .ConfigureAwait(false);
        var channelId = Interlocked.Increment(ref _nextChannelId);
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var pending = new ToolkitPendingCall(onEvent, onData);
        _calls[channelId] = pending;
        try {
            await SendAsync(
                    ToolkitWireCodec.CreateFrame(
                        ToolkitFrameKind.Request,
                        ToolkitOpcode.Call,
                        channelId,
                        requestId,
                        ToolkitRequestCodec.Encode(request)),
                    cancellationToken)
                .ConfigureAwait(false);
            _ = pending.Completion.Task.ContinueWith(
                completed => _calls.TryRemove(channelId, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return new ToolkitTerminalSession(
                this,
                pending,
                channelId,
                requestId);
        } catch {
            _calls.TryRemove(channelId, out _);
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        if (_socket is { State: WebSocketState.Open or WebSocketState.CloseReceived }) {
            try {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        null,
                        timeout.Token)
                    .ConfigureAwait(false);
            } catch (Exception error) when (error is WebSocketException or ObjectDisposedException or OperationCanceledException) {
            }
        }

        _stop.Cancel();
        _socket?.Dispose();
        if (_receiveTask is not null) {
            try {
                await _receiveTask.ConfigureAwait(false);
            } catch (Exception error) when (error is OperationCanceledException or WebSocketException or ObjectDisposedException) {
            }
        }

        _connectGate.Dispose();
        _sendGate.Dispose();
        _stop.Dispose();
    }

    private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken) {
        if (_socket?.State == WebSocketState.Open) {
            return;
        }

        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_socket?.State == WebSocketState.Open) {
                return;
            }

            var socket = new ClientWebSocket();
            socket.Options.AddSubProtocol("arkt.bin");
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            await socket.ConnectAsync(_endpoint.Uri, cancellationToken).ConfigureAwait(false);
            try {
                Hello = await NegotiateAsync(socket, cancellationToken).ConfigureAwait(false);
                _socket = socket;
                _receiveTask = Task.Run(() => ReceiveLoopAsync(socket), CancellationToken.None);
            } catch {
                socket.Dispose();
                throw;
            }
        } finally {
            _connectGate.Release();
        }
    }

    private async ValueTask<ToolkitHello> NegotiateAsync(ClientWebSocket socket, CancellationToken cancellationToken) {
        var clientName = Environment.GetEnvironmentVariable(
            "ARKT_CLIENT_NAME");
        if (string.IsNullOrWhiteSpace(clientName)) {
            clientName = Environment.MachineName;
        }

        await ToolkitWebSocketProtocol.SendAsync(
                socket,
                ToolkitWireCodec.HelloRequest(
                    1,
                    ToolkitVersion.Current,
                    _token,
                    clientName),
                cancellationToken)
            .ConfigureAwait(false);
        var response = await ToolkitWebSocketProtocol.ReceiveAsync(socket, cancellationToken).ConfigureAwait(false)
            ?? throw new IOException("The toolkit server closed during negotiation.");
        if (response.Kind != ToolkitFrameKind.Response || response.Opcode != ToolkitOpcode.Hello) {
            throw new InvalidDataException("The toolkit server returned an invalid handshake.");
        }

        var status = (ToolkitStatus)response.Status;
        if (status != ToolkitStatus.Ok) {
            throw status == ToolkitStatus.Unauthorized
                ? new UnauthorizedAccessException("Toolkit server authentication failed.")
                : new InvalidOperationException($"Toolkit protocol negotiation failed: {status}.");
        }

        var hello = ToolkitWireCodec.DecodeHello(response)
            ?? throw new InvalidDataException("The toolkit server hello is invalid.");
        if (hello.ProtocolMajor != ToolkitProtocolVersion.Major) {
            throw new InvalidOperationException($"Unsupported toolkit protocol major version {hello.ProtocolMajor}.");
        }

        if (hello.ProtocolMinor < ToolkitProtocolVersion.Minor) {
            throw new InvalidOperationException(
                $"Toolkit protocol {hello.ProtocolMajor}." +
                $"{hello.ProtocolMinor} is older than the required " +
                $"{ToolkitProtocolVersion.Major}." +
                $"{ToolkitProtocolVersion.Minor}.");
        }

        return hello;
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket) {
        try {
            while (!_stop.IsCancellationRequested) {
                var frame = await ToolkitWebSocketProtocol.ReceiveAsync(socket, _stop.Token).ConfigureAwait(false);
                if (frame is null) {
                    break;
                }

                if (!_calls.TryGetValue(frame.Value.ChannelId, out var pending)) {
                    continue;
                }

                switch (frame.Value.Kind) {
                    case ToolkitFrameKind.Notify when frame.Value.Opcode == ToolkitOpcode.Log:
                        pending.Report(new ToolkitEvent(
                            ToolkitWireCodec.GetLogSource(frame.Value),
                            ToolkitWireCodec.GetLogMessage(frame.Value)));
                        break;
                    case ToolkitFrameKind.Notify when frame.Value.Opcode == ToolkitOpcode.Progress:
                        pending.Report(ToolkitWireCodec.GetProgress(frame.Value));
                        break;
                    case ToolkitFrameKind.Data:
                        pending.ReceiveData(frame.Value.Payload);
                        break;
                    case ToolkitFrameKind.AttachmentEnd:
                        pending.CompleteDownload();
                        break;
                    case ToolkitFrameKind.Response:
                        var status = (ToolkitStatus)frame.Value.Status;
                        var error = status == ToolkitStatus.Ok
                            ? null
                            : ToolkitWireCodec.GetErrorMessage(frame.Value) ??
                                (pending.HadError ? null : "Toolkit operation failed.");
                        pending.Completion.TrySetResult(new ToolkitResult(
                            status == ToolkitStatus.Ok,
                            status == ToolkitStatus.Ok ? 0 : 1,
                            error,
                            status == ToolkitStatus.Ok ? pending.TakeDownload() : null));
                        break;
                }
            }
        } catch (Exception error) when (
            error is WebSocketException or
            IOException or
            OperationCanceledException or
            ObjectDisposedException) {
            foreach (var call in _calls.Values) {
                call.Completion.TrySetException(error);
            }
        } finally {
            foreach (var call in _calls.Values) {
                call.Completion.TrySetException(new IOException("The toolkit WebSocket connection closed."));
            }
        }
    }

    private async ValueTask SendAttachmentsAsync(uint channelId, uint requestId,
        IReadOnlyList<ToolkitAttachment> attachments, ToolkitAttachmentOpener? openAttachment,
        Action<ToolkitEvent>? onTransferEvent,
        CancellationToken cancellationToken) {
        if (attachments.Count == 0) {
            return;
        }

        if (openAttachment is null) {
            throw new InvalidOperationException("The toolkit request requires attachments.");
        }

        var buffer = new byte[128 * 1024];
        foreach (var attachment in attachments) {
            onTransferEvent?.Invoke(new ToolkitEvent(
                "status",
                $"Uploading {attachment.FileName}"));
            await using var source = await openAttachment(attachment).ConfigureAwait(false);
            long total = 0;
            while (true) {
                var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                total = checked(total + read);
                if (total > attachment.Length) {
                    throw new InvalidDataException("The attachment exceeded its declared size.");
                }

                await SendAsync(ToolkitWireCodec.Data(channelId, requestId, buffer.AsMemory(0, read)), cancellationToken)
                    .ConfigureAwait(false);
                onTransferEvent?.Invoke(new ToolkitEvent(
                    "progress",
                    attachment.FileName,
                    total,
                    attachment.Length));
            }

            if (total != attachment.Length) {
                throw new InvalidDataException("The attachment length changed before transmission.");
            }

            await SendAsync(ToolkitWireCodec.AttachmentEnd(channelId, requestId), cancellationToken)
                .ConfigureAwait(false);
            onTransferEvent?.Invoke(new ToolkitEvent(
                "success",
                $"{attachment.FileName} uploaded"));
        }
    }

    private async ValueTask SendAsync(ToolkitFrame frame, CancellationToken cancellationToken) {
        var socket = _socket;
        if (socket?.State != WebSocketState.Open) {
            throw new IOException("The toolkit WebSocket is not connected.");
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await ToolkitWebSocketProtocol.SendAsync(socket, frame, cancellationToken).ConfigureAwait(false);
        } finally {
            _sendGate.Release();
        }
    }

    internal ValueTask SendTerminalDataAsync(
        uint channelId,
        uint requestId,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) =>
        SendAsync(
            ToolkitWireCodec.Data(
                channelId,
                requestId,
                data),
            cancellationToken);

    internal async ValueTask CloseTerminalAsync(
        uint channelId,
        uint requestId) {
        await TrySendAsync(
                ToolkitWireCodec.Close(channelId, requestId))
            .ConfigureAwait(false);
    }

    private async ValueTask TrySendAsync(ToolkitFrame frame) {
        try {
            await SendAsync(frame, CancellationToken.None).ConfigureAwait(false);
        } catch (Exception error) when (error is WebSocketException or IOException or ObjectDisposedException) {
        }
    }
}
