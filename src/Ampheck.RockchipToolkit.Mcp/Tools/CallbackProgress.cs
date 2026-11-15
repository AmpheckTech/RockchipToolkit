namespace Ampheck.RockchipToolkit.Mcp.Tools;

internal sealed class CallbackProgress(
    Action<long> callback) : IProgress<long> {
    public void Report(long value) => callback(value);
}
