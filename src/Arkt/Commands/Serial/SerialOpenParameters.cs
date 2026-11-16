using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Commands.Serial.Parser;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial;

internal sealed class SerialOpenParameters : SerialCommandParameters {
    [CommandParam(
        ["--baudrate", "--baud", "-b"],
        Description = "Baud rate.")]
    public int BaudRate { get; init; } = SerialPortDefaults.BaudRate;

    [CommandParam(
        ["--data-bits"],
        Description = "Data bits: 5, 6, 7, or 8.",
        Parser = typeof(SerialDataBitsParser),
        Choices = ["5", "6", "7", "8"])]
    public int DataBits { get; init; } = 8;

    [CommandParam(
        ["--parity"],
        Description = "none, odd, even, mark, or space.",
        Parser = typeof(SerialParityParser),
        Choices = ["none", "odd", "even", "mark", "space"])]
    public SerialParity Parity { get; init; } =
        SerialParity.None;

    [CommandParam(
        ["--stop-bits"],
        Description = "one, one-point-five, or two.",
        Parser = typeof(SerialStopBitsParser),
        Choices = ["one", "one-point-five", "two"])]
    public SerialStopBits StopBits { get; init; } =
        SerialStopBits.One;

    [CommandParam(
        ["--handshake"],
        Description = "none, xon-xoff, rts-cts, or rts-cts-xon-xoff.",
        Parser = typeof(SerialHandshakeParser),
        Choices = [
            "none",
            "xon-xoff",
            "rts-cts",
            "rts-cts-xon-xoff"
        ])]
    public SerialHandshake Handshake { get; init; } =
        SerialHandshake.None;

    [CommandParam(
        ["--no-dtr"],
        Description = "Leave DTR disabled.")]
    public bool NoDtr { get; init; }

    [CommandParam(
        ["--no-rts"],
        Description = "Leave RTS disabled.")]
    public bool NoRts { get; init; }
}
