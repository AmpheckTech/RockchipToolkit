namespace Ampheck.RockchipToolkit.Rpc;

internal sealed class ToolkitPendingCall(
    Action<ToolkitEvent>? onEvent,
    Action<ReadOnlyMemory<byte>>? onData = null) {
    private readonly MemoryStream _download = new();

    public TaskCompletionSource<ToolkitResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool HadError { get; private set; }
    public bool HasDownload => _download.Length > 0;

    public void Report(ToolkitEvent value) {
        if (Completion.Task.IsCompleted) {
            return;
        }

        HadError |= value.Source == "stderr";
        try {
            onEvent?.Invoke(value);
        } catch (Exception error) {
            Completion.TrySetException(error);
        }
    }

    public void AppendDownload(ReadOnlySpan<byte> data) => _download.Write(data);

    public void CompleteDownload() => _download.Position = 0;

    public void ReceiveData(ReadOnlyMemory<byte> data) {
        if (Completion.Task.IsCompleted) {
            return;
        }

        try {
            if (onData is null) {
                _download.Write(data.Span);
            } else {
                onData(data);
            }
        } catch (Exception error) {
            Completion.TrySetException(error);
        }
    }

    public byte[]? TakeDownload() => _download.Length == 0 ? null : _download.ToArray();
}
