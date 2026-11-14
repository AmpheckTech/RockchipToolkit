using System.Text.Json;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Rpc;

internal sealed partial class JsonRpcConnection {
    private async Task HandleTaskRequestAsync(
        JsonRpcRequest request) {
        try {
            switch (request.Method) {
                case JsonRpcMethods.TaskList: {
                        var parameters = Deserialize<TaskListParams>(
                            request.Params);
                        var result = new TaskListResult {
                            Tasks = _tasks.List(
                                parameters.IncludeCompleted)
                        };
                        await SendResultAsync(
                                request.Id,
                                Serialize(result),
                                taskId: null,
                                _stop.Token)
                            .ConfigureAwait(false);
                        return;
                    }
                case JsonRpcMethods.TaskGet: {
                        var parameters = Deserialize<TaskGetParams>(
                            request.Params);
                        await SendResultAsync(
                                request.Id,
                                Serialize(_tasks.Get(
                                    parameters.TaskId)),
                                taskId: null,
                                _stop.Token)
                            .ConfigureAwait(false);
                        return;
                    }
                case JsonRpcMethods.TaskCancel: {
                        var parameters = Deserialize<TaskCancelParams>(
                            request.Params);
                        var result = new TaskCancelResult {
                            Cancelled = _tasks.Cancel(
                                parameters.TaskId)
                        };
                        await SendResultAsync(
                                request.Id,
                                Serialize(result),
                                taskId: null,
                                _stop.Token)
                            .ConfigureAwait(false);
                        return;
                    }
                case JsonRpcMethods.TaskSubscribe: {
                        var parameters =
                            Deserialize<TaskSubscribeParams>(
                                request.Params);
                        _tasks.Subscribe(
                            _connectionId,
                            parameters.TaskId,
                            SendTaskEventAsync);
                        await SendResultAsync(
                                request.Id,
                                Serialize(new EmptyResult()),
                                taskId: null,
                                _stop.Token)
                            .ConfigureAwait(false);
                        return;
                    }
                default:
                    await SendErrorAsync(
                            request.Id,
                            JsonRpcErrorCodes.MethodNotFound,
                            $"Unknown JSON-RPC method '{request.Method}'.",
                            taskId: null,
                            _stop.Token)
                        .ConfigureAwait(false);
                    return;
            }
        } catch (ToolkitRpcException error) {
            await TrySendErrorAsync(
                    request.Id,
                    error.Code,
                    error.Message,
                    taskId: null)
                .ConfigureAwait(false);
        } catch (JsonException error) {
            await TrySendErrorAsync(
                    request.Id,
                    JsonRpcErrorCodes.InvalidParams,
                    error.Message,
                    taskId: null)
                .ConfigureAwait(false);
        } catch (Exception error) {
            _logger.LogWarning(
                "Task request failed: method={Method} message={Message}",
                request.Method,
                error.Message);
            _logger.LogDebug(
                error,
                "Task request failure details");
            await TrySendErrorAsync(
                    request.Id,
                    JsonRpcErrorCodes.InternalError,
                    error.Message,
                    taskId: null)
                .ConfigureAwait(false);
        }
    }
}
