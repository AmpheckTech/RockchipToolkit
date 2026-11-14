namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public static class JsonRpcErrorCodes {
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int Cancelled = -32800;
    public const int Unauthorized = -32001;
    public const int NotFound = -32004;
    public const int Conflict = -32009;
}
