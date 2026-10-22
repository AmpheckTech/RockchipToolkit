namespace Ampheck.Serial;

public sealed record SerialPortOptions {
    public required string PortName { get; init; }
    public int BaudRate { get; init; } = 1500000;
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
        if (BaudRate <= 0 ||
            DataBits is < 5 or > 8) {
            throw new ArgumentOutOfRangeException(
                nameof(SerialPortOptions),
                "Serial port format values are invalid.");
        }

        if (!OperatingSystem.IsWindows() &&
            StopBits == SerialStopBits.OnePointFive) {
            throw new ArgumentException(
                "1.5 stop bits are not supported on Linux.",
                nameof(SerialPortOptions));
        }
    }
}
