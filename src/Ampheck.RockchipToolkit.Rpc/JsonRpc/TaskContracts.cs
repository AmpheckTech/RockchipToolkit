namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record TaskEventNotification {
    public required string TaskId { get; init; }
    public required string Method { get; init; }
    public required string Kind { get; init; }
    public required string Message { get; init; }
    public long? Current { get; init; }
    public long? Total { get; init; }
}

public sealed record TaskInfo {
    public required string TaskId { get; init; }
    public required string Method { get; init; }
    public required string State { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public long? Current { get; init; }
    public long? Total { get; init; }
}

public sealed record TaskListParams {
    public bool IncludeCompleted { get; init; }
}

public sealed record TaskListResult {
    public required TaskInfo[] Tasks { get; init; }
}

public sealed record TaskGetParams {
    public required string TaskId { get; init; }
}

public sealed record TaskCancelParams {
    public required string TaskId { get; init; }
}

public sealed record TaskCancelResult {
    public bool Cancelled { get; init; }
}

public sealed record TaskSubscribeParams {
    public required string TaskId { get; init; }
}
