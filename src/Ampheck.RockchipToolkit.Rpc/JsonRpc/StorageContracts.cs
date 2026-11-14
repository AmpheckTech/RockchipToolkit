namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record FirmwareFlashParams {
    public required DeviceTarget Target { get; init; }
    public string? FirmwareBlobId { get; init; }
    public string? FirmwareUrl { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
    public bool ResetAfterFlash { get; init; } = true;
}

public sealed record StorageReadParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
    public uint Sector { get; init; }
    public ushort Count { get; init; } = 1;
}

public sealed record StorageWriteParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
    public string? BlobId { get; init; }
    public string? SourceUrl { get; init; }
    public uint Sector { get; init; }
    public long? Length { get; init; }
    public long? SourceOffset { get; init; }
    public bool EraseBeforeWrite { get; init; } = true;
    public bool Backup { get; init; }
}

public sealed record StorageWriteResult {
    public BlobReference? Backup { get; init; }
    public long BytesWritten { get; init; }
}

public sealed record StorageEraseParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
    public uint Sector { get; init; }
    public ushort Count { get; init; }
}

public sealed record StorageSelectParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
    public required ToolkitStorage Storage { get; init; }
}
