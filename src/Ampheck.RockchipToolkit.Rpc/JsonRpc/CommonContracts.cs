namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record EmptyParams;

public sealed record EmptyResult;

public sealed record DeviceTarget {
    public ToolkitTransport Transport { get; init; } =
        ToolkitTransport.Auto;
    public ushort? VendorId { get; init; }
    public ushort? ProductId { get; init; }
}

public sealed record TextResult {
    public required string Text { get; init; }
}

public sealed record BlobReference {
    public required string BlobId { get; init; }
    public required string FileName { get; init; }
    public long Length { get; init; }
    public string? Sha256 { get; init; }
}

public sealed record ClientInfo(
    string Name,
    string Peer,
    string ConnectionId);
