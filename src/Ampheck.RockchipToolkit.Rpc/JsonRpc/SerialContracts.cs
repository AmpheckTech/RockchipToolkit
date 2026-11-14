namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record SerialListResult {
    public required string[] Ports { get; init; }
}

public sealed record SerialPortSettings {
    public int BaudRate { get; init; } = 150_000;
    public int DataBits { get; init; } = 8;
    public ToolkitSerialParity Parity { get; init; } =
        ToolkitSerialParity.None;
    public ToolkitSerialStopBits StopBits { get; init; } =
        ToolkitSerialStopBits.One;
    public ToolkitSerialHandshake Handshake { get; init; } =
        ToolkitSerialHandshake.None;
    public bool DtrEnable { get; init; } = true;
    public bool RtsEnable { get; init; } = true;
}

public sealed record SerialOpenParams {
    public required string PortName { get; init; }
    public required SerialPortSettings Settings { get; init; }
}

public sealed record SerialCloseParams {
    public required string PortName { get; init; }
}

public sealed record SerialCloseResult {
    public bool Closed { get; init; }
}

public sealed record SerialPortParams {
    public required string PortName { get; init; }
}

public sealed record SerialClearResult {
    public bool Cleared { get; init; }
}

public sealed record SerialTerminalParams {
    public required string PortName { get; init; }
    public int HistoryBytes { get; init; } = 256 * 1024;
}

public sealed record StreamSessionResult {
    public required string SessionId { get; init; }
}

public sealed record SerialHistoryParams {
    public required string PortName { get; init; }
    public int MaximumBytes { get; init; } = 256 * 1024;
}
