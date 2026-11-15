namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed class JsonRpcCallContext {
    public required string RequestId { get; init; }
    public required string TaskId { get; init; }
    public required ClientInfo Client { get; init; }
    public bool IsLocalClient { get; init; }
    public required CancellationToken CancellationToken { get; init; }
    public required Func<TaskEventNotification, ValueTask> Report { get; init; }

    public ValueTask StatusAsync(string message) =>
        ReportAsync("status", message);

    public ValueTask OutputAsync(string message) =>
        ReportAsync("stdout", message);

    public ValueTask ErrorAsync(string message) =>
        ReportAsync("stderr", message);

    public ValueTask ProgressAsync(
        string label,
        long current,
        long total) =>
        Report(new TaskEventNotification {
            TaskId = TaskId,
            Method = string.Empty,
            Kind = "progress",
            Message = label,
            Current = current,
            Total = total
        });

    private ValueTask ReportAsync(string kind, string message) =>
        Report(new TaskEventNotification {
            TaskId = TaskId,
            Method = string.Empty,
            Kind = kind,
            Message = message
        });
}
