using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed partial class ToolkitRpcClient : IAsyncDisposable {
    private const int MaximumMessageBytes = 1024 * 1024;
    private readonly ToolkitEndpoint _endpoint;
    private readonly string? _token;
    private readonly string _clientName;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, PendingCall> _calls =
        new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private ClientWebSocket? _socket;
    private Task? _receiveTask;
    private int _disposed;

    public ToolkitRpcClient(
        ToolkitEndpoint endpoint,
        string? token = null,
        string? clientName = null) {
        _endpoint = endpoint ??
            throw new ArgumentNullException(nameof(endpoint));
        _token = token;
        _clientName = string.IsNullOrWhiteSpace(clientName)
            ? Environment.MachineName
            : clientName.Trim();
    }

    public async ValueTask<TResult> RequestAsync<TParams, TResult>(
        string method,
        TParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var requestId = Guid.NewGuid().ToString("N");
        var taskId = Guid.NewGuid().ToString("N");
        var pending = new PendingCall(taskId, onEvent);
        _calls[requestId] = pending;
        var parametersElement = JsonSerializer.SerializeToElement(
            parameters,
            GetTypeInfo<TParams>());
        var request = new JsonRpcRequest {
            Id = requestId,
            Method = method,
            Params = parametersElement,
            TaskId = taskId
        };
        try {
            await SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            var response = await pending.Completion.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            if (response.Error is { } error) {
                throw new ToolkitRpcException(
                    error.Code,
                    error.Message);
            }

            if (response.Result is not { } result) {
                throw new ToolkitRpcException(
                    JsonRpcErrorCodes.InternalError,
                    "The JSON-RPC response contains no result.");
            }

            return JsonSerializer.Deserialize(
                    result,
                    GetTypeInfo<TResult>())
                ?? throw new ToolkitRpcException(
                    JsonRpcErrorCodes.InternalError,
                    "The JSON-RPC result is invalid.");
        } catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested) {
            await TryCancelTaskAsync(taskId).ConfigureAwait(false);
            throw;
        } finally {
            _calls.TryRemove(requestId, out _);
        }
    }

    public async ValueTask<ClientWebSocket> OpenStreamAsync<TParams>(
        string method,
        TParams parameters,
        string pathPrefix,
        string protocol,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) {
        var session = await RequestAsync<TParams, StreamSessionResult>(
                method,
                parameters,
                onEvent,
                cancellationToken)
            .ConfigureAwait(false);
        return await ConnectWebSocketAsync(
                $"{pathPrefix}/{session.SessionId}",
                protocol,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        if (_socket is { State: WebSocketState.Open }) {
            try {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(
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

        await _stop.CancelAsync().ConfigureAwait(false);
        _socket?.Dispose();
        if (_receiveTask is not null) {
            try {
                await _receiveTask.ConfigureAwait(false);
            } catch (Exception error) when (
                error is OperationCanceledException or
                WebSocketException or
                IOException) {
            }
        }

        foreach (var call in _calls.Values) {
            call.Completion.TrySetException(
                new IOException(
                    "The JSON-RPC connection was closed."));
        }

        _connectGate.Dispose();
        _sendGate.Dispose();
        _stop.Dispose();
    }

    private async ValueTask ConnectAsync(
        CancellationToken cancellationToken) {
        if (_socket?.State == WebSocketState.Open) {
            return;
        }

        await _connectGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            if (_socket?.State == WebSocketState.Open) {
                return;
            }

            _socket?.Dispose();
            var socket = await ConnectWebSocketAsync(
                    _endpoint.Path,
                    "arkt.rpc.v1",
                    cancellationToken)
                .ConfigureAwait(false);
            _socket = socket;
            _receiveTask = ReceiveAsync(socket, _stop.Token);
        } finally {
            _connectGate.Release();
        }
    }

    private async ValueTask<ClientWebSocket> ConnectWebSocketAsync(
        string path,
        string protocol,
        CancellationToken cancellationToken) {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(protocol);
        socket.Options.SetRequestHeader(
            "X-ARKT-Client",
            _clientName);
        if (!string.IsNullOrEmpty(_token)) {
            socket.Options.SetRequestHeader(
                "Authorization",
                $"Bearer {_token}");
        }

        try {
            var uri = new UriBuilder(
                "ws",
                _endpoint.Host,
                _endpoint.Port,
                path).Uri;
            await socket.ConnectAsync(uri, cancellationToken)
                .ConfigureAwait(false);
            return socket;
        } catch {
            socket.Dispose();
            throw;
        }
    }

    private async Task ReceiveAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken) {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try {
            while (!cancellationToken.IsCancellationRequested) {
                using var message = new MemoryStream();
                while (true) {
                    var result = await socket.ReceiveAsync(
                            buffer,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (result.MessageType ==
                        WebSocketMessageType.Close) {
                        return;
                    }

                    if (result.MessageType !=
                        WebSocketMessageType.Text) {
                        throw new InvalidDataException(
                            "The JSON-RPC WebSocket returned a non-text message.");
                    }

                    message.Write(buffer, 0, result.Count);
                    if (message.Length > MaximumMessageBytes) {
                        throw new InvalidDataException(
                            "The JSON-RPC message is too large.");
                    }

                    if (result.EndOfMessage) {
                        break;
                    }
                }

                DispatchMessage(
                    Encoding.UTF8.GetString(
                        message.GetBuffer(),
                        0,
                        checked((int)message.Length)));
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
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void DispatchMessage(string json) {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(
                "method",
                out _)) {
            var notification = JsonSerializer.Deserialize(
                json,
                GetTypeInfo<JsonRpcNotification>());
            if (notification?.Method == "task.event") {
                var value = JsonSerializer.Deserialize(
                    notification.Params,
                    GetTypeInfo<TaskEventNotification>());
                if (value is not null &&
                    _calls.Values.FirstOrDefault(call =>
                        call.TaskId == value.TaskId) is { } pending) {
                    pending.OnEvent?.Invoke(value);
                }
            }

            return;
        }

        var response = JsonSerializer.Deserialize(
            json,
            GetTypeInfo<JsonRpcResponse>());
        if (response is not null &&
            _calls.TryGetValue(response.Id, out var call)) {
            call.Completion.TrySetResult(response);
        }
    }

    private async ValueTask SendAsync<T>(
        T value,
        CancellationToken cancellationToken) {
        var socket = _socket;
        if (socket?.State != WebSocketState.Open) {
            throw new IOException(
                "The JSON-RPC WebSocket is not connected.");
        }

        var json = JsonSerializer.Serialize(
            value,
            GetTypeInfo<T>());
        await _sendGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await socket.SendAsync(
                    Encoding.UTF8.GetBytes(json),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            _sendGate.Release();
        }
    }

    private async ValueTask TryCancelTaskAsync(string taskId) {
        try {
            await RequestAsync<TaskCancelParams, TaskCancelResult>(
                    JsonRpcMethods.TaskCancel,
                    new TaskCancelParams { TaskId = taskId },
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is IOException or
            WebSocketException or
            ToolkitRpcException) {
        }
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>() =>
        ToolkitJsonSerializerContext.Default.GetTypeInfo(
            typeof(T)) as JsonTypeInfo<T>
        ?? throw new InvalidOperationException(
            $"No JSON serializer metadata exists for {typeof(T)}.");

    private sealed class PendingCall(
        string taskId,
        Action<TaskEventNotification>? onEvent) {
        public TaskCompletionSource<JsonRpcResponse> Completion {
            get;
        } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public Action<TaskEventNotification>? OnEvent { get; } =
            onEvent;
        public string TaskId { get; } = taskId;
    }

}
