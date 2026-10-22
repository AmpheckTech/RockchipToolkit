using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class CommandOptions {
    public TransportKind Transport { get; set; } = TransportKind.Auto;
    public InputFetchLocation FetchLocation { get; set; } =
        InputFetchLocation.Server;
    public ushort? VendorId { get; set; }
    public ushort? ProductId { get; set; }
    public string? Loader { get; set; }
    public string? Source { get; set; }
    public string? File { get; set; }
    public string? Backup { get; set; }
    public string? AdbSerial { get; set; }
    public string? AdbShellCommand { get; set; }
    public string? AdbSource { get; set; }
    public string? AdbDestination { get; set; }
    public string? CompletionShell { get; set; }
    public string? ServerEndpoint { get; set; }
    public string? ServerListen { get; set; }
    public string? ServerAccessToken { get; set; }
    public string? ServerHostName { get; set; }
    public int? DiscoverySeconds { get; set; }
    public int? ServerIdleSeconds { get; set; }
    public int? ServerMaxAttachmentMiB { get; set; }
    public int? ServerMaxRequestMiB { get; set; }
    public int? ServerSerialHistoryKiB { get; set; }
    public string? SerialPort { get; set; }
    public int? SerialBaudRate { get; set; }
    public int? SerialDataBits { get; set; }
    public ToolkitSerialParity SerialParity { get; set; } =
        ToolkitSerialParity.None;
    public ToolkitSerialStopBits SerialStopBits { get; set; } =
        ToolkitSerialStopBits.One;
    public ToolkitSerialHandshake SerialHandshake { get; set; } =
        ToolkitSerialHandshake.None;
    public bool SerialDtrEnable { get; set; } = true;
    public bool SerialRtsEnable { get; set; } = true;
    public int? SerialHistoryBytes { get; set; }
    public uint? Sector { get; set; }
    public long? WriteLength { get; set; }
    public long? WriteSourceOffset { get; set; }
    public ushort? Count { get; set; }
    public ToolkitResetMode? Reset { get; set; }
    public ToolkitStorage? Storage { get; set; }
    public bool EraseBeforeWrite { get; set; } = true;
    public bool ResetAfterFlash { get; set; } = true;
}
