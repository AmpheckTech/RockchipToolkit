using System.Collections.Concurrent;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Server.Tasks;

internal sealed class TaskManager : IDisposable {
    private const int MaximumRetainedTasks = 512;
    private readonly object _sync = new();
    private readonly Dictionary<string, TaskRecord> _tasks =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<
        long,
        ConcurrentDictionary<
            string,
            Func<TaskEventNotification, ValueTask>>> _subscribers = [];
    private bool _disposed;

    public TaskRecord Start(
        string taskId,
        string method,
        CancellationToken serverToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        lock (_sync) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_tasks.ContainsKey(taskId)) {
                throw new ToolkitRpcException(
                    JsonRpcErrorCodes.Conflict,
                    $"Task '{taskId}' already exists.");
            }

            var record = new TaskRecord(
                taskId,
                method,
                CancellationTokenSource.CreateLinkedTokenSource(
                    serverToken));
            _tasks.Add(taskId, record);
            PruneCompleted();
            return record;
        }
    }

    public bool Cancel(string taskId) {
        TaskRecord? record;
        lock (_sync) {
            _tasks.TryGetValue(taskId, out record);
        }

        if (record is null ||
            record.Snapshot().State != "running") {
            return false;
        }

        record.Cancel();
        return true;
    }

    public TaskInfo Get(string taskId) {
        lock (_sync) {
            return _tasks.TryGetValue(taskId, out var record)
                ? record.Snapshot()
                : throw new ToolkitRpcException(
                    JsonRpcErrorCodes.NotFound,
                    $"Task '{taskId}' was not found.");
        }
    }

    public TaskInfo[] List(bool includeCompleted) {
        lock (_sync) {
            return _tasks.Values
                .Select(record => record.Snapshot())
                .Where(info => includeCompleted ||
                    info.State == "running")
                .OrderByDescending(info => info.StartedAt)
                .ToArray();
        }
    }

    public void Subscribe(
        long connectionId,
        string taskId,
        Func<TaskEventNotification, ValueTask> report) {
        _ = Get(taskId);
        var subscriptions = _subscribers.GetOrAdd(
            connectionId,
            _ => new ConcurrentDictionary<
                string,
                Func<TaskEventNotification, ValueTask>>(
                StringComparer.Ordinal));
        subscriptions[taskId] = report;
    }

    public void Unsubscribe(long connectionId) {
        _subscribers.TryRemove(connectionId, out _);
    }

    public async ValueTask PublishAsync(
        TaskEventNotification value) {
        var reports = _subscribers.Values
            .Select(subscriptions =>
                subscriptions.TryGetValue(
                    value.TaskId,
                    out var report)
                    ? report
                    : null)
            .Where(report => report is not null)
            .Select(report => report!)
            .ToArray();
        foreach (var report in reports) {
            try {
                await report(value).ConfigureAwait(false);
            } catch (Exception error) when (
                error is IOException or
                System.Net.WebSockets.WebSocketException or
                ObjectDisposedException) {
            }
        }
    }

    public void Dispose() {
        TaskRecord[] records;
        lock (_sync) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            records = _tasks.Values.ToArray();
            _tasks.Clear();
        }

        foreach (var record in records) {
            record.Dispose();
        }

        _subscribers.Clear();
    }

    private void PruneCompleted() {
        var excess = _tasks.Count - MaximumRetainedTasks;
        if (excess <= 0) {
            return;
        }

        var completed = _tasks.Values
            .Where(record =>
                record.Snapshot().State != "running")
            .OrderBy(record => record.StartedAt)
            .Take(excess)
            .ToArray();
        foreach (var record in completed) {
            _tasks.Remove(record.TaskId);
            record.Dispose();
        }
    }
}

internal sealed class TaskRecord : IDisposable {
    private readonly object _sync = new();
    private readonly CancellationTokenSource _stop;
    private string? _message;
    private string? _error;
    private long? _current;
    private long? _total;
    private DateTimeOffset? _completedAt;
    private string _state = "running";
    private int _disposed;

    public TaskRecord(
        string taskId,
        string method,
        CancellationTokenSource stop) {
        TaskId = taskId;
        Method = method;
        StartedAt = DateTimeOffset.UtcNow;
        _stop = stop;
    }

    public string TaskId { get; }
    public string Method { get; }
    public DateTimeOffset StartedAt { get; }
    public CancellationToken CancellationToken => _stop.Token;

    public void Update(TaskEventNotification value) {
        lock (_sync) {
            _message = value.Message;
            _current = value.Current;
            _total = value.Total;
        }
    }

    public void Complete(string? error, bool cancelled) {
        lock (_sync) {
            _state = cancelled
                ? "cancelled"
                : error is null
                    ? "completed"
                    : "failed";
            _error = error;
            _completedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Cancel() {
        try {
            _stop.Cancel();
        } catch (ObjectDisposedException) {
        }
    }

    public TaskInfo Snapshot() {
        lock (_sync) {
            return new TaskInfo {
                TaskId = TaskId,
                Method = Method,
                State = _state,
                StartedAt = StartedAt,
                CompletedAt = _completedAt,
                Message = _message,
                Error = _error,
                Current = _current,
                Total = _total
            };
        }
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        _stop.Dispose();
    }
}
