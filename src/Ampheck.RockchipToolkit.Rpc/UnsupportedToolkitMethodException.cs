namespace Ampheck.RockchipToolkit.Rpc;

public sealed class UnsupportedToolkitMethodException(string method) : Exception($"Unsupported toolkit method '{method}'.") {
    public string Method { get; } = method;
}
