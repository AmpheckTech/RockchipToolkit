namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed class ToolkitRpcException(
    int code,
    string message,
    Exception? innerException = null) : Exception(message, innerException) {
    public int Code { get; } = code;
}
