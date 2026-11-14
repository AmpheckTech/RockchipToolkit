using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

/// <summary>Runs toolkit, serial, and ADB startup operations.</summary>
internal sealed partial class ToolkitConnection : IAsyncDisposable {
    private async Task ExecuteChannelAsync(ToolkitChannel channel) {
        if (channel.Request.Operation ==
            ToolkitOperation.AdbServerEnsure) {
            await ExecuteAdbServerEnsureAsync(channel)
                .ConfigureAwait(false);
            return;
        }

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

    private async Task ExecuteAdbServerEnsureAsync(
        ToolkitChannel channel) {
        try {
            var operationStop = channel.StartOperation(_stop.Token);
            await _adb.EnsureRunningAsync(operationStop.Token)
                .ConfigureAwait(false);
            await SendAsync(
                    ToolkitWireCodec.Response(
                        channel.ChannelId,
                        channel.RequestId,
                        ToolkitStatus.Ok),
                    CancellationToken.None)
                .ConfigureAwait(false);
        } catch (OperationCanceledException) {
            await TrySendAsync(
                    ToolkitWireCodec.ErrorResponse(
                        channel.ChannelId,
                        channel.RequestId,
                        ToolkitStatus.Cancelled,
                        "ADB server startup was cancelled."))
                .ConfigureAwait(false);
        } catch (Exception error) {
            _logger.LogDebug(
                error,
                "Official ADB server is unavailable on port {Port}",
                _options.AdbPort);
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
        Ampheck.RockchipToolkit.Transport.Serial.SerialSession session,
        CancellationToken cancellationToken) {
        await foreach (var value in session.Events.ReadAllAsync(
            cancellationToken)) {
            if (value.Kind == Ampheck.RockchipToolkit.Transport.Serial.SerialEventKind.Data) {
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
        Ampheck.RockchipToolkit.Transport.Serial.SerialSession session,
        CancellationToken cancellationToken) {
        await foreach (var data in channel.Input.ReadAllAsync(
            cancellationToken)) {
            await session.WriteAsync(
                    data,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
