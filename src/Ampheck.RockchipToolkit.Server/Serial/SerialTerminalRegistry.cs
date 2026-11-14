using System.Collections.Concurrent;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Transport.Serial;

namespace Ampheck.RockchipToolkit.Server.Serial;

internal sealed class SerialTerminalRegistry(ToolkitSerialService serial) {

    private static readonly TimeSpan Lifetime =
        TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, PendingTerminal>
        _pending = new(StringComparer.Ordinal);

    public StreamSessionResult Register(
        SerialTerminalParams parameters) {
        RemoveExpired();
        var id = Guid.NewGuid().ToString("N");
        _pending[id] = new PendingTerminal(
            parameters.PortName,
            parameters.HistoryBytes,
            DateTimeOffset.UtcNow);
        return new StreamSessionResult { SessionId = id };
    }

    public async ValueTask<SerialTerminalLease> TakeAsync(
        string sessionId,
        CancellationToken cancellationToken) {
        if (!_pending.TryRemove(sessionId, out var pending) ||
            DateTimeOffset.UtcNow - pending.CreatedAt > Lifetime) {
            throw new ToolkitRpcException(
                JsonRpcErrorCodes.NotFound,
                $"Serial terminal session '{sessionId}' was not found.");
        }

        var history = serial.GetHistory(new SerialHistoryParams {
            PortName = pending.PortName,
            MaximumBytes = pending.HistoryBytes
        });
        var session = await serial.AttachAsync(pending.PortName)
            .ConfigureAwait(false);
        return new SerialTerminalLease(history, session);
    }

    private void RemoveExpired() {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _pending) {
            if (now - pair.Value.CreatedAt > Lifetime) {
                _pending.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record PendingTerminal(
        string PortName,
        int HistoryBytes,
        DateTimeOffset CreatedAt);
}

internal sealed class SerialTerminalLease(byte[] history, SerialSession session) : IAsyncDisposable {
    public byte[] History { get; } = history;
    public SerialSession Session { get; } = session;

    public ValueTask DisposeAsync() => Session.DisposeAsync();
}
