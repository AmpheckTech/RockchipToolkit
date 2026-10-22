namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitRequest {
    public required ToolkitOperation Operation { get; init; }
    public ToolkitTransport Transport { get; init; } =
        ToolkitTransport.Auto;
    public ushort? VendorId { get; init; }
    public ushort? ProductId { get; init; }
    public string? Loader { get; init; }
    public ToolkitInputKind LoaderLocation { get; init; } =
        ToolkitInputKind.LocalPath;
    public ToolkitInputKind SourceLocation { get; init; } =
        ToolkitInputKind.LocalPath;
    public ToolkitInputKind FileLocation { get; init; } =
        ToolkitInputKind.LocalPath;
    public string? Source { get; init; }
    public string? File { get; init; }
    public string? Backup { get; init; }
    public string? SerialPort { get; init; }
    public int? BaudRate { get; init; }
    public int? SerialDataBits { get; init; }
    public ToolkitSerialParity SerialParity { get; init; } =
        ToolkitSerialParity.None;
    public ToolkitSerialStopBits SerialStopBits { get; init; } =
        ToolkitSerialStopBits.One;
    public ToolkitSerialHandshake SerialHandshake { get; init; } =
        ToolkitSerialHandshake.None;
    public bool SerialDtrEnable { get; init; } = true;
    public bool SerialRtsEnable { get; init; } = true;
    public int? SerialHistoryBytes { get; init; }
    public uint? Sector { get; init; }
    public long? WriteLength { get; init; }
    public long? WriteSourceOffset { get; init; }
    public ushort? Count { get; init; }
    public ToolkitResetMode? ResetMode { get; init; }
    public ToolkitStorage? Storage { get; init; }
    public bool EraseBeforeWrite { get; init; } = true;
    public bool ResetAfterFlash { get; init; } = true;
    public bool Verbose { get; init; }
    public IReadOnlyList<ToolkitAttachment> Attachments { get; init; } =
        [];
}
