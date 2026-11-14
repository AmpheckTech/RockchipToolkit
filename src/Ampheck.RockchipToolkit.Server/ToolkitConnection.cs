using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

internal sealed partial class ToolkitConnection : IAsyncDisposable {
    private const int MaximumChannels = 64;
    private readonly WebSocket _socket;
    private readonly ToolkitHostOptions _options;
    private readonly IToolkitExecutor _executor;
    private readonly ToolkitSerialService _serial;
    private readonly AdbServerProcess _adb;
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
        AdbServerProcess adb,
        SemaphoreSlim operationGate,
        ILogger logger,
        string? peer,
        CancellationToken serverToken) {
        _socket = socket;
        _options = options;
        _executor = executor;
        _serial = serial;
        _adb = adb;
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
        var channels = _channels.Values.ToArray();
        _channels.Clear();
        Exception? failure = null;
        foreach (var channel in channels) {
            try {
                await channel.DisposeAsync().ConfigureAwait(false);
            } catch (Exception error) {
                failure ??= error;
            }
        }

        try {
            _sendGate.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _stop.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _socket.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
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
        await SendAsync(
                ToolkitWireCodec.HelloResponse(
                    frame.Value.RequestId,
                    ToolkitStatus.Ok,
                    hello),
                _stop.Token)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Client connected: client={ClientName} peer={Peer} connection={ConnectionId}",
            _clientName,
            _peer,
            _connectionId);
        return true;
    }
}
