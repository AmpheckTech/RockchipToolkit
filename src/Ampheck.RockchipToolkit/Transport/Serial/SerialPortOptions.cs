namespace Ampheck.RockchipToolkit.Transport.Serial;

public sealed record SerialPortOptions {
    public required string PortName { get; init; }
    public int BaudRate { get; init; } =
        SerialPortDefaults.BaudRate;
    public int DataBits { get; init; } = 8;
    public SerialParity Parity { get; init; } =
        SerialParity.None;
    public SerialStopBits StopBits { get; init; } =
        SerialStopBits.One;
    public SerialHandshake Handshake { get; init; } =
        SerialHandshake.None;
    public bool DtrEnable { get; init; } = true;
    public bool RtsEnable { get; init; } = true;

    public void Validate() {
        ArgumentException.ThrowIfNullOrWhiteSpace(PortName);
        if (BaudRate <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(BaudRate),
                "The serial baud rate must be positive.");
        }

        if (DataBits is < 5 or > 8) {
            throw new ArgumentOutOfRangeException(
                nameof(DataBits),
                "Serial data bits must be between 5 and 8.");
        }

        if (!OperatingSystem.IsWindows() &&
            StopBits == SerialStopBits.OnePointFive) {
            throw new ArgumentException(
                "1.5 stop bits are not supported on Linux.",
                nameof(SerialPortOptions));
        }
    }
}
