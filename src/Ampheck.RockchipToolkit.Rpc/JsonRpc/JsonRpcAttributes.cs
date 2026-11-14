namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public enum JsonRpcServiceLifetime {
    Scoped,
    Singleton,
    Transient
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class JsonRpcServiceAttribute(
    JsonRpcServiceLifetime lifetime = JsonRpcServiceLifetime.Scoped)
    : Attribute {
    public JsonRpcServiceLifetime Lifetime { get; } = lifetime;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class JsonRpcMethodAttribute(
    string name,
    bool longRunning = false) : Attribute {
    public string Name { get; } = name;
    public bool LongRunning { get; } = longRunning;
}
