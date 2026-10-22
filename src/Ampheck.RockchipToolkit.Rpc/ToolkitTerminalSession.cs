namespace Ampheck.RockchipToolkit.Rpc;

public sealed class ToolkitTerminalSession : IAsyncDisposable {
    private readonly ToolkitClient _client;
    private readonly ToolkitPendingCall _pending;
    private readonly uint _channelId;
    private readonly uint _requestId;
    private int _disposed;

    internal ToolkitTerminalSession(
        ToolkitClient client,
        ToolkitPendingCall pending,
        uint channelId,
        uint requestId) {
        _client = client;
        _pending = pending;
        _channelId = channelId;
        _requestId = requestId;
    }

    public Task<ToolkitResult> Completion =>
        _pending.Completion.Task;

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) =>
        _client.SendTerminalDataAsync(
            _channelId,
            _requestId,
            data,
            cancellationToken);

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        await _client.CloseTerminalAsync(
                _channelId,
                _requestId)
            .ConfigureAwait(false);
    }
}
