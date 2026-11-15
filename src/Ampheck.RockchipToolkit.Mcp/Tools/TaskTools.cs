using System.ComponentModel;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol.Server;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

[McpServerToolType]
public sealed class TaskTools(ToolkitRpcClient client) {
    [McpServerTool(
        Name = "list_tasks",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("List toolkit operations running on the server.")]
    public async Task<TaskListResult> ListTasks(
        bool includeCompleted = false,
        CancellationToken cancellationToken = default) {
        return await client.RequestAsync<
            TaskListParams,
            TaskListResult>(
                JsonRpcMethods.TaskList,
                new TaskListParams {
                    IncludeCompleted = includeCompleted
                },
                cancellationToken: cancellationToken);
    }

    [McpServerTool(
        Name = "get_task",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("Get the current state of a toolkit operation.")]
    public async Task<TaskInfo> GetTask(
        string taskId,
        CancellationToken cancellationToken = default) {
        return await client.RequestAsync<TaskGetParams, TaskInfo>(
            JsonRpcMethods.TaskGet,
            new TaskGetParams { TaskId = taskId },
            cancellationToken: cancellationToken);
    }

    [McpServerTool(
        Name = "cancel_task",
        Destructive = true,
        UseStructuredContent = true)]
    [Description("Cancel a running toolkit operation.")]
    public async Task<TaskCancelResult> CancelTask(
        string taskId,
        CancellationToken cancellationToken = default) {
        return await client.RequestAsync<
            TaskCancelParams,
            TaskCancelResult>(
                JsonRpcMethods.TaskCancel,
                new TaskCancelParams { TaskId = taskId },
                cancellationToken: cancellationToken);
    }
}
