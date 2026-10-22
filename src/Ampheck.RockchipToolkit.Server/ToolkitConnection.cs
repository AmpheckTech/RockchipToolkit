using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

internal sealed class ToolkitConnection : IAsyncDisposable {
    private const int MaximumChannels = 64;
    private readonly WebSocket _socket;
    private readonly ToolkitHostOptions _options;
    private readonly IToolkitExecutor _executor;
    private readonly ToolkitSerialService _serial;
    private readonly SemaphoreSlim _operationGate;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ILogger _logger;
    private readonly string _peer;
    private readonly ConcurrentDictionary<uint, ToolkitChannel> _channels = [];
    private readonly CancellationTokenSource _stop;
    private readonly string _connectionId = Guid.NewGuid().ToString("D");
    private string? _clientName;

    public ToolkitConnection(
        WebSocket socket,
        ToolkitHostOptions options,
        IToolkitExecutor executor,
        ToolkitSerialService serial,
        SemaphoreSlim operationGate,
        ILogger logger,
        string? peer,
        CancellationToken serverToken) {
        _socket = socket;
        _options = options;
        _executor = executor;
        _serial = serial;
        _operationGate = operationGate;
        _logger = logger;
        _peer = peer ?? "unknown";
        _stop = CancellationTokenSource.CreateLinkedTokenSource(serverToken);
    }

    public async Task RunAsync() {
        try {
            if (!await AuthenticateAsync().ConfigureAwait(false)) {
                return;
            }

            while (!_stop.IsCancellationRequested) {
                var frame = await ToolkitWebSocketProtocol.ReceiveAsync(_socket, _stop.Token)
                    .ConfigureAwait(false);
                if (frame is null) {
                    return;
                }

                await DispatchAsync(frame.Value).ConfigureAwait(false);
            }
        } catch (Exception error) when (
            error is WebSocketException or
            IOException or
            OperationCanceledException or
            InvalidDataException) {
            _logger.LogDebug(
                "Toolkit WebSocket client disconnected: {ErrorMessage}",
                error.Message);
        } finally {
            _stop.Cancel();
            foreach (var channel in _channels.Values) {
                channel.Cancel();
            }
        }
    }

    public async ValueTask DisposeAsync() {
        foreach (var channel in _channels.Values) {
            await channel.DisposeAsync().ConfigureAwait(false);
        }

        _channels.Clear();
        _sendGate.Dispose();
        _stop.Dispose();
        _socket.Dispose();
    }

    private async ValueTask<bool> AuthenticateAsync() {
        var frame = await ToolkitWebSocketProtocol.ReceiveAsync(_socket, _stop.Token).ConfigureAwait(false);
        if (frame is null ||
            frame.Value.Kind != ToolkitFrameKind.Request ||
            frame.Value.Opcode != ToolkitOpcode.Hello ||
            frame.Value.ProtocolMajor != ToolkitProtocolVersion.Major ||
            frame.Value.ProtocolMinor <
                ToolkitProtocolVersion.Minor) {
            await SendAsync(ToolkitWireCodec.HelloResponse(
                    frame?.RequestId ?? 0,
                    ToolkitStatus.UnsupportedProtocol,
                    hello: null),
                _stop.Token)
                .ConfigureAwait(false);
            return false;
        }

        var reader = new ToolkitTlvReader(frame.Value.Payload);
        _clientName = NormalizeClientName(reader.GetString(
            ToolkitWireTag.ClientName));
        if (!TokenMatches(reader.GetString(ToolkitWireTag.Token))) {
            _logger.LogWarning(
                "Authentication failed: client={ClientName} peer={Peer}",
                _clientName,
                _peer);
            await SendAsync(ToolkitWireCodec.HelloResponse(
                    frame.Value.RequestId,
                    ToolkitStatus.Unauthorized,
                    hello: null),
                _stop.Token)
                .ConfigureAwait(false);
            return false;
        }

        var hello = new ToolkitHello(
            _options.InstanceId,
            _connectionId,
            _options.HostName,
            ToolkitVersion.Current,
            ToolkitProtocolVersion.Major,
            ToolkitProtocolVersion.Minor,
            ToolkitOperationNames.All,
            ToolkitCapabilities.All);
        await SendAsync(ToolkitWireCodec.HelloResponse(frame.Value.RequestId, ToolkitStatus.Ok, hello), _stop.Token)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Client connected: client={ClientName} peer={Peer} connection={ConnectionId}",
            _clientName,
            _peer,
            _connectionId);
        return true;
    }

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
            ValidateRemoteInputs(request);
            ValidateAttachmentLimits(request.Attachments);
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
            "Operation requested: client={ClientName} connection={ConnectionId} channel={ChannelId} operation={Operation} target={Target}",
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

    private async Task ExecuteChannelAsync(ToolkitChannel channel) {
        if (channel.Request.Operation is
            ToolkitOperation.SerialList or
            ToolkitOperation.SerialOpen or
            ToolkitOperation.SerialClose or
            ToolkitOperation.SerialHistory or
            ToolkitOperation.SerialTerminal) {
            await ExecuteSerialChannelAsync(channel)
                .ConfigureAwait(false);
            return;
        }

        try {
            var started = Stopwatch.GetTimestamp();
            if (_operationGate.CurrentCount == 0) {
                _logger.LogInformation(
                    "Operation queued: client={ClientName} channel={ChannelId} operation={Operation}",
                    _clientName,
                    channel.ChannelId,
                    ToolkitOperationNames.GetName(
                        channel.Request.Operation));
            }

            await _operationGate.WaitAsync(_stop.Token).ConfigureAwait(false);
            try {
                var operationStop = channel.StartOperation(_stop.Token);
                async ValueTask ReportAsync(ToolkitEvent value) {
                    if (value.Source == "progress" &&
                        value.Current is { } current &&
                        value.Total is { } total) {
                        await SendAsync(
                                ToolkitWireCodec.Progress(
                                    channel.ChannelId,
                                    channel.RequestId,
                                    channel.NextSequence(),
                                    value.Message,
                                    current,
                                    total),
                                operationStop.Token)
                            .ConfigureAwait(false);
                        return;
                    }

                    await SendAsync(ToolkitWireCodec.Notify(
                            channel.ChannelId,
                            channel.RequestId,
                            channel.NextSequence(),
                            value.Source,
                            value.Message),
                        operationStop.Token)
                        .ConfigureAwait(false);
                }

                var result = await _executor.ExecuteAsync(channel.Request, ReportAsync, operationStop.Token)
                    .ConfigureAwait(false);
                if (result.Success && channel.DownloadPath is { } downloadPath && File.Exists(downloadPath)) {
                    await SendDownloadAsync(channel, downloadPath, operationStop.Token).ConfigureAwait(false);
                }

                var response = result.Success
                    ? ToolkitWireCodec.Response(channel.ChannelId, channel.RequestId, ToolkitStatus.Ok)
                    : result.Error is null
                        ? ToolkitWireCodec.Response(channel.ChannelId, channel.RequestId, ToolkitStatus.Internal)
                        : ToolkitWireCodec.ErrorResponse(channel.ChannelId, channel.RequestId,
                            ToolkitStatus.Internal, result.Error);
                _logger.LogInformation(
                    "Operation completed: client={ClientName} " +
                    "channel={ChannelId} operation={Operation} " +
                    "success={Success} elapsedMs={ElapsedMs:F1}",
                    _clientName,
                    channel.ChannelId,
                    ToolkitOperationNames.GetName(channel.Request.Operation),
                    result.Success,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                await SendAsync(response, CancellationToken.None).ConfigureAwait(false);
            } finally {
                _operationGate.Release();
            }
        } catch (OperationCanceledException) {
            await TrySendAsync(ToolkitWireCodec.ErrorResponse(channel.ChannelId, channel.RequestId,
                ToolkitStatus.Cancelled, "Toolkit operation was cancelled.")).ConfigureAwait(false);
        } catch (Exception error) {
            LogOperationFailure(
                error,
                channel,
                "Operation failed");
            await TrySendAsync(ToolkitWireCodec.ErrorResponse(channel.ChannelId, channel.RequestId,
                ToolkitStatus.Internal, error.Message)).ConfigureAwait(false);
        } finally {
            _channels.TryRemove(channel.ChannelId, out _);
            await channel.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ExecuteSerialChannelAsync(
        ToolkitChannel channel) {
        var started = Stopwatch.GetTimestamp();
        try {
            var operationStop = channel.StartOperation(_stop.Token);
            switch (channel.Request.Operation) {
                case ToolkitOperation.SerialList:
                    foreach (var device in _serial.ListDevices()) {
                        await SendAsync(
                                ToolkitWireCodec.Notify(
                                    channel.ChannelId,
                                    channel.RequestId,
                                    channel.NextSequence(),
                                    "stdout",
                                    device.PortName),
                                operationStop.Token)
                            .ConfigureAwait(false);
                    }

                    break;
                case ToolkitOperation.SerialOpen:
                    await SendStatusAsync(
                            channel,
                            operationStop.Token,
                            $"Opening serial port " +
                            $"{channel.Request.SerialPort}")
                        .ConfigureAwait(false);
                    await _serial.OpenPortAsync(channel.Request)
                        .ConfigureAwait(false);
                    await SendSuccessAsync(
                            channel,
                            operationStop.Token,
                            $"Serial port {channel.Request.SerialPort} opened")
                        .ConfigureAwait(false);
                    break;
                case ToolkitOperation.SerialClose:
                    var closed = await _serial.CloseAsync(
                            channel.Request.SerialPort)
                        .ConfigureAwait(false);
                    await SendSuccessAsync(
                            channel,
                            operationStop.Token,
                            closed
                                ? $"Serial port " +
                                    $"{channel.Request.SerialPort} closed"
                                : $"Serial port " +
                                    $"{channel.Request.SerialPort} was not open")
                        .ConfigureAwait(false);
                    break;
                case ToolkitOperation.SerialHistory:
                    var history = _serial.GetHistory(channel.Request);
                    if (history.Length > 0) {
                        await SendAsync(
                                ToolkitWireCodec.Data(
                                    channel.ChannelId,
                                    channel.RequestId,
                                    history),
                                operationStop.Token)
                            .ConfigureAwait(false);
                        await SendAsync(
                                ToolkitWireCodec.AttachmentEnd(
                                    channel.ChannelId,
                                    channel.RequestId),
                                operationStop.Token)
                            .ConfigureAwait(false);
                    }

                    break;
                case ToolkitOperation.SerialTerminal:
                    await RunSerialTerminalAsync(
                            channel,
                            operationStop,
                            channel.Request)
                        .ConfigureAwait(false);
                    break;
            }

            await SendAsync(
                    ToolkitWireCodec.Response(
                        channel.ChannelId,
                        channel.RequestId,
                        ToolkitStatus.Ok),
                    CancellationToken.None)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Serial operation completed: client={ClientName} " +
                "channel={ChannelId} operation={Operation} " +
                "target={Target} elapsedMs={ElapsedMs:F1}",
                _clientName,
                channel.ChannelId,
                ToolkitOperationNames.GetName(channel.Request.Operation),
                DescribeTarget(channel.Request),
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        } catch (OperationCanceledException) {
            await TrySendAsync(
                    ToolkitWireCodec.ErrorResponse(
                        channel.ChannelId,
                        channel.RequestId,
                        ToolkitStatus.Cancelled,
                        "Serial operation was cancelled."))
                .ConfigureAwait(false);
        } catch (Exception error) {
            LogOperationFailure(
                error,
                channel,
                "Serial operation failed");
            await TrySendAsync(
                    ToolkitWireCodec.ErrorResponse(
                        channel.ChannelId,
                        channel.RequestId,
                        ToolkitStatus.Internal,
                        error.Message))
                .ConfigureAwait(false);
        } finally {
            _channels.TryRemove(channel.ChannelId, out _);
            await channel.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunSerialTerminalAsync(
        ToolkitChannel channel,
        CancellationTokenSource operationStop,
        ToolkitRequest request) {
        await using var session = await _serial.AttachAsync(request)
            .ConfigureAwait(false);
        var output = PumpSerialOutputAsync(
            channel,
            session,
            operationStop.Token);
        var input = PumpSerialInputAsync(
            channel,
            session,
            operationStop.Token);
        var completed = await Task.WhenAny(input, output)
            .ConfigureAwait(false);
        await operationStop.CancelAsync().ConfigureAwait(false);
        if (completed == input) {
            await input.ConfigureAwait(false);
            try {
                await output.ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        } else {
            await output.ConfigureAwait(false);
            try {
                await input.ConfigureAwait(false);
            } catch (OperationCanceledException) {
            }
        }
    }

    private async Task PumpSerialOutputAsync(
        ToolkitChannel channel,
        Ampheck.Serial.SerialSession session,
        CancellationToken cancellationToken) {
        await foreach (var value in session.Events.ReadAllAsync(
            cancellationToken)) {
            if (value.Kind == Ampheck.Serial.SerialEventKind.Data) {
                await SendAsync(
                        ToolkitWireCodec.Data(
                            channel.ChannelId,
                            channel.RequestId,
                            value.Data),
                        cancellationToken)
                    .ConfigureAwait(false);
            } else if (value.Message is not null) {
                await SendAsync(
                        ToolkitWireCodec.Notify(
                            channel.ChannelId,
                            channel.RequestId,
                            channel.NextSequence(),
                            "stderr",
                            value.Message),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task PumpSerialInputAsync(
        ToolkitChannel channel,
        Ampheck.Serial.SerialSession session,
        CancellationToken cancellationToken) {
        await foreach (var data in channel.Input.ReadAllAsync(
            cancellationToken)) {
            await session.WriteAsync(
                    data,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

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

    private async ValueTask SendDownloadAsync(ToolkitChannel channel, string path, CancellationToken cancellationToken) {
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

    private void LogOperationFailure(
        Exception error,
        ToolkitChannel channel,
        string message) {
        if (error is IOException or
            InvalidOperationException or
            InvalidDataException or
            ArgumentException or
            TimeoutException or
            UnauthorizedAccessException or
            NotSupportedException or
            HttpRequestException) {
            _logger.LogWarning(
                "{Message}: client={ClientName} channel={ChannelId} " +
                "operation={Operation} target={Target} error={ErrorMessage}",
                message,
                _clientName,
                channel.ChannelId,
                ToolkitOperationNames.GetName(
                    channel.Request.Operation),
                DescribeTarget(channel.Request),
                error.Message);
            return;
        }

        _logger.LogError(
            error,
            "{Message}: client={ClientName} channel={ChannelId} " +
            "operation={Operation} target={Target}",
            message,
            _clientName,
            channel.ChannelId,
            ToolkitOperationNames.GetName(
                channel.Request.Operation),
            DescribeTarget(channel.Request));
    }

    private static string DescribeTarget(
        ToolkitRequest request) {
        if (!string.IsNullOrWhiteSpace(request.SerialPort)) {
            return request.SerialPort;
        }

        if (!string.IsNullOrWhiteSpace(request.Source)) {
            return DescribeLocation(request.Source);
        }

        if (!string.IsNullOrWhiteSpace(request.File)) {
            return DescribeLocation(request.File);
        }

        if (request.VendorId.HasValue ||
            request.ProductId.HasValue) {
            return $"{request.VendorId ?? 0:X4}:" +
                $"{request.ProductId ?? 0:X4}";
        }

        return "-";
    }

    private static string DescribeLocation(string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https"
            ? uri.GetLeftPart(UriPartial.Path)
            : location;

    private static string NormalizeClientName(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "unknown";
        }

        var characters = value
            .Take(80)
            .Select(character =>
                char.IsControl(character) ? ' ' : character)
            .ToArray();
        var normalized = new string(characters).Trim();
        return normalized.Length == 0 ? "unknown" : normalized;
    }

    private static void ValidateRemoteInputs(
        ToolkitRequest request) {
        if (request.Attachments
            .Select(attachment => attachment.Role)
            .Distinct()
            .Count() != request.Attachments.Count) {
            throw new InvalidDataException(
                "The remote request contains duplicate attachment roles.");
        }

        if (request.Operation == ToolkitOperation.Flash) {
            ValidateInput(
                "firmware",
                request.SourceLocation,
                request.Source,
                ToolkitAttachmentRole.Source,
                request.Attachments);
        }

        if (request.Operation == ToolkitOperation.Loader) {
            ValidateInput(
                "loader",
                request.FileLocation,
                request.File,
                ToolkitAttachmentRole.Loader,
                request.Attachments);
        }

        if (request.Operation == ToolkitOperation.Write) {
            ValidateInput(
                "write input",
                request.FileLocation,
                request.File,
                ToolkitAttachmentRole.Input,
                request.Attachments);
        }

        if (request.Loader is not null) {
            ValidateInput(
                "loader",
                request.LoaderLocation,
                request.Loader,
                ToolkitAttachmentRole.Loader,
                request.Attachments);
        }
    }

    private void ValidateAttachmentLimits(
        IReadOnlyList<ToolkitAttachment> attachments) {
        var limits = _options.TransferLimits;
        if (attachments.Count > limits.MaximumAttachments) {
            throw new InvalidDataException(
                $"The request contains more than " +
                $"{limits.MaximumAttachments} attachments.");
        }

        long total = 0;
        foreach (var attachment in attachments) {
            if (!Enum.IsDefined(attachment.Role) ||
                !Enum.IsDefined(attachment.Kind) ||
                string.IsNullOrWhiteSpace(attachment.FileName) ||
                attachment.FileName.Length > 255 ||
                attachment.FileName.Any(char.IsControl) ||
                attachment.FileName is "." or ".." ||
                attachment.FileName.Contains('/') ||
                attachment.FileName.Contains('\\')) {
                throw new InvalidDataException(
                    "The request contains an invalid attachment descriptor.");
            }

            if (attachment.Length < 0 ||
                attachment.Length > limits.MaximumAttachmentBytes) {
                throw new InvalidDataException(
                    $"Attachment '{attachment.FileName}' exceeds the " +
                    $"configured size limit.");
            }

            if (attachment.Length >
                limits.MaximumRequestBytes - total) {
                throw new InvalidDataException(
                    "The request attachments exceed the configured " +
                    "total size limit.");
            }

            total += attachment.Length;
        }
    }

    private static void ValidateInput(
        string description,
        ToolkitInputKind location,
        string? value,
        ToolkitAttachmentRole role,
        IReadOnlyList<ToolkitAttachment> attachments) {
        if (location == ToolkitInputKind.LocalPath) {
            throw new InvalidDataException(
                $"A remote request cannot use a server-local " +
                $"{description} path.");
        }

        if (location == ToolkitInputKind.Attachment) {
            var attachment = attachments.FirstOrDefault(
                item => item.Role == role);
            if (attachment is null) {
                throw new InvalidDataException(
                    $"The remote request declares an attachment-backed " +
                    $"{description}, but no matching attachment is present.");
            }

            if (role != ToolkitAttachmentRole.Source &&
                attachment.Kind != ToolkitAttachmentKind.File) {
                throw new InvalidDataException(
                    $"The {description} attachment must be a file.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidDataException(
                $"The remote request contains no {description}.");
        }
    }
}
