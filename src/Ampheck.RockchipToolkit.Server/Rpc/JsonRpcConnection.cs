using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Generated;
using Ampheck.RockchipToolkit.Server.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Rpc;

internal sealed partial class JsonRpcConnection : IAsyncDisposable {
    private static readonly JsonElement EmptyParameters =
        JsonDocument.Parse("{}").RootElement.Clone();
    private readonly WebSocket _socket;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskManager _tasks;
    private readonly ILogger _logger;
    private readonly ClientInfo _client;
    private readonly long _connectionId;
    private readonly CancellationTokenSource _stop;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, Task> _requests = [];
    private int _disposed;

    public JsonRpcConnection(
        WebSocket socket,
        IServiceScopeFactory scopeFactory,
        TaskManager tasks,
        ClientInfo client,
        long connectionId,
        ILogger logger,
        CancellationToken serverToken) {
        _socket = socket;
        _scopeFactory = scopeFactory;
        _tasks = tasks;
        _client = client;
        _connectionId = connectionId;
        _logger = logger;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(
            serverToken);
    }

    public async Task RunAsync() {
        try {
            while (!_stop.IsCancellationRequested) {
                JsonRpcRequest? request;
                try {
                    request = await JsonWebSocketProtocol.ReceiveAsync(
                            _socket,
                            _stop.Token)
                        .ConfigureAwait(false);
                } catch (JsonException error) {
                    await SendErrorAsync(
                            string.Empty,
                            JsonRpcErrorCodes.ParseError,
                            error.Message,
                            taskId: null,
                            _stop.Token)
                        .ConfigureAwait(false);
                    continue;
                }

                if (request is null) {
                    return;
                }

                var method = request.Method;
                var task = HandleRequestAsync(request);
                _requests[task.Id.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)] =
                    task;
                _ = task.ContinueWith(
                    completed => {
                        _ = completed.Exception;
                        _requests.TryRemove(
                            completed.Id.ToString(
                                System.Globalization.CultureInfo.InvariantCulture),
                            out _);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        } catch (Exception error) when (
            error is WebSocketException or
            IOException or
            OperationCanceledException or
            InvalidDataException) {
            _logger.LogDebug(
                "JSON-RPC connection ended: {ErrorMessage}",
                error.Message);
        } finally {
            await _stop.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(_requests.Values.ToArray())
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        _tasks.Unsubscribe(_connectionId);
        await _stop.CancelAsync().ConfigureAwait(false);
        _sendGate.Dispose();
        _stop.Dispose();
        _socket.Dispose();
    }

    private async Task HandleRequestAsync(
        JsonRpcRequest request) {
        var started = Stopwatch.GetTimestamp();
        if (!string.Equals(
                request.Version,
                "2.0",
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.Method)) {
            await SendErrorAsync(
                    request.Id,
                    JsonRpcErrorCodes.InvalidRequest,
                    "The JSON-RPC request is invalid.",
                    taskId: request.TaskId,
                    _stop.Token)
                .ConfigureAwait(false);
            return;
        }

        if (request.Method.StartsWith(
                "tasks.",
                StringComparison.Ordinal)) {
            await HandleTaskRequestAsync(request)
                .ConfigureAwait(false);
            return;
        }

        var longRunning = GeneratedJsonRpcCatalog.IsLongRunning(
            request.Method);
        var taskId = !string.IsNullOrWhiteSpace(request.TaskId)
            ? request.TaskId
            : longRunning
                ? Guid.NewGuid().ToString("N")
                : $"{_connectionId}-{request.Id}";
        TaskRecord? record = null;
        CancellationTokenSource? localStop = null;
        try {
            CancellationToken operationToken;
            if (longRunning) {
                record = _tasks.Start(
                    taskId,
                    request.Method,
                    _stop.Token);
                operationToken = record.CancellationToken;
                _tasks.Subscribe(
                    _connectionId,
                    taskId,
                    SendTaskEventAsync);
            } else {
                localStop = CancellationTokenSource
                    .CreateLinkedTokenSource(_stop.Token);
                operationToken = localStop.Token;
            }

            async ValueTask ReportAsync(
                TaskEventNotification value) {
                var normalized = value with {
                    TaskId = taskId,
                    Method = request.Method
                };
                record?.Update(normalized);
                await _tasks.PublishAsync(normalized)
                    .ConfigureAwait(false);
            }

            var context = new JsonRpcCallContext {
                RequestId = request.Id,
                TaskId = taskId,
                Client = _client,
                CancellationToken = operationToken,
                Report = ReportAsync
            };
            await using var scope =
                _scopeFactory.CreateAsyncScope();
            var parameters = request.Params.ValueKind is
                JsonValueKind.Undefined or JsonValueKind.Null
                    ? EmptyParameters
                    : request.Params;
            var result = await GeneratedJsonRpcCatalog.InvokeAsync(
                    scope.ServiceProvider,
                    request.Method,
                    parameters,
                    context)
                .ConfigureAwait(false);
            record?.Complete(error: null, cancelled: false);
            await SendResultAsync(
                    request.Id,
                    result,
                    longRunning ? taskId : null,
                    _stop.Token)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "RPC completed: client={ClientName} method={Method} " +
                "task={TaskId} elapsedMs={ElapsedMs:F1}",
                _client.Name,
                request.Method,
                taskId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        } catch (OperationCanceledException) {
            record?.Complete(error: null, cancelled: true);
            await TrySendErrorAsync(
                    request.Id,
                    JsonRpcErrorCodes.Cancelled,
                    "Operation cancelled.",
                    longRunning ? taskId : null)
                .ConfigureAwait(false);
        } catch (ToolkitRpcException error) {
            record?.Complete(error.Message, cancelled: false);
            _logger.LogWarning(
                "RPC failed: client={ClientName} method={Method} " +
                "message={Message}",
                _client.Name,
                request.Method,
                error.Message);
            await TrySendErrorAsync(
                    request.Id,
                    error.Code,
                    error.Message,
                    longRunning ? taskId : null)
                .ConfigureAwait(false);
        } catch (Exception error) {
            record?.Complete(error.Message, cancelled: false);
            _logger.LogWarning(
                "RPC failed: client={ClientName} method={Method} " +
                "message={Message}",
                _client.Name,
                request.Method,
                error.Message);
            _logger.LogDebug(
                error,
                "RPC failure details");
            await TrySendErrorAsync(
                    request.Id,
                    JsonRpcErrorCodes.InternalError,
                    error.Message,
                    longRunning ? taskId : null)
                .ConfigureAwait(false);
        } finally {
            localStop?.Dispose();
            record?.Dispose();
        }
    }

    private async ValueTask SendTaskEventAsync(
        TaskEventNotification value) {
        var notification = new JsonRpcNotification {
            Method = "task.event",
            Params = Serialize(value)
        };
        await SendAsync(notification, _stop.Token)
            .ConfigureAwait(false);
    }

    private async ValueTask SendResultAsync(
        string id,
        JsonElement result,
        string? taskId,
        CancellationToken cancellationToken) {
        await SendAsync(
                new JsonRpcResponse {
                    Id = id,
                    Result = result,
                    TaskId = taskId
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask SendErrorAsync(
        string id,
        int code,
        string message,
        string? taskId,
        CancellationToken cancellationToken) {
        await SendAsync(
                new JsonRpcResponse {
                    Id = id,
                    Error = new JsonRpcError {
                        Code = code,
                        Message = message
                    },
                    TaskId = taskId
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TrySendErrorAsync(
        string id,
        int code,
        string message,
        string? taskId) {
        try {
            await SendErrorAsync(
                    id,
                    code,
                    message,
                    taskId,
                    CancellationToken.None)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is WebSocketException or
            IOException or
            ObjectDisposedException) {
        }
    }

    private async ValueTask SendAsync<T>(
        T value,
        CancellationToken cancellationToken) {
        await _sendGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await JsonWebSocketProtocol.SendAsync(
                    _socket,
                    value,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            _sendGate.Release();
        }
    }

    private static T Deserialize<T>(JsonElement parameters) {
        var value = JsonSerializer.Deserialize(
            parameters.ValueKind is
                JsonValueKind.Undefined or JsonValueKind.Null
                ? EmptyParameters
                : parameters,
            typeof(T),
            ToolkitJsonSerializerContext.Default);
        return value is T typed
            ? typed
            : throw new ToolkitRpcException(
                JsonRpcErrorCodes.InvalidParams,
                "Invalid method parameters.");
    }

    private static JsonElement Serialize<T>(T value) =>
        JsonSerializer.SerializeToElement(
            value,
            typeof(T),
            ToolkitJsonSerializerContext.Default);
}
