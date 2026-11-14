using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record JsonRpcRequest {
    [JsonPropertyName("jsonrpc")]
    public string Version { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public JsonElement Params { get; init; }

    [JsonPropertyName("taskId")]
    public string? TaskId { get; init; }
}

public sealed record JsonRpcResponse {
    [JsonPropertyName("jsonrpc")]
    public string Version { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }

    [JsonPropertyName("taskId")]
    public string? TaskId { get; init; }
}

public sealed record JsonRpcNotification {
    [JsonPropertyName("jsonrpc")]
    public string Version { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public JsonElement Params { get; init; }
}

public sealed record JsonRpcError {
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}
