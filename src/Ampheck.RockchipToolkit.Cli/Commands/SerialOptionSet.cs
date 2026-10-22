using System.CommandLine;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class SerialOptionSet {
    public Option<int> Baud { get; } = new(
        "--baudrate",
        "--baud",
        "-b") {
        Description = "Baud rate.",
        DefaultValueFactory = _ => 1500000
    };
    public Option<string?> DataBits { get; } = new("--data-bits") {
        Description = "Data bits: 5, 6, 7, or 8.",
        DefaultValueFactory = _ => "8"
    };
    public Option<string?> Parity { get; } = new("--parity") {
        Description = "none, odd, even, mark, or space.",
        DefaultValueFactory = _ => "none"
    };
    public Option<string?> StopBits { get; } = new("--stop-bits") {
        Description = "one, one-point-five, or two.",
        DefaultValueFactory = _ => "one"
    };
    public Option<string?> Handshake { get; } = new("--handshake") {
        Description = "none, xon-xoff, rts-cts, or rts-cts-xon-xoff.",
        DefaultValueFactory = _ => "none"
    };
    public Option<bool> NoDtr { get; } = new("--no-dtr") {
        Description = "Leave DTR disabled."
    };
    public Option<bool> NoRts { get; } = new("--no-rts") {
        Description = "Leave RTS disabled."
    };

    public void AddTo(Command command) {
        Parity.AcceptOnlyFromAmong(
            "none", "odd", "even", "mark", "space");
        StopBits.AcceptOnlyFromAmong(
            "one", "one-point-five", "two");
        Handshake.AcceptOnlyFromAmong(
            "none",
            "xon-xoff",
            "rts-cts",
            "rts-cts-xon-xoff");
        command.Add(Baud);
        command.Add(DataBits);
        command.Add(Parity);
        command.Add(StopBits);
        command.Add(Handshake);
        command.Add(NoDtr);
        command.Add(NoRts);
    }

    public void Apply(
        CommandOptions target,
        ParseResult parsed,
        string portName) {
        target.SerialPort = portName;
        target.SerialBaudRate = parsed.GetValue(Baud);
        if (target.SerialBaudRate is not > 0) {
            throw new ArgumentException(
                "--baudrate must be greater than zero.");
        }

        var dataBits = int.Parse(
            parsed.GetValue(DataBits) ?? "8");
        if (dataBits is < 5 or > 8) {
            throw new ArgumentException(
                "--data-bits must be 5, 6, 7, or 8.");
        }

        target.SerialDataBits = dataBits;
        target.SerialParity = parsed.GetValue(Parity) switch {
            "odd" => ToolkitSerialParity.Odd,
            "even" => ToolkitSerialParity.Even,
            "mark" => ToolkitSerialParity.Mark,
            "space" => ToolkitSerialParity.Space,
            _ => ToolkitSerialParity.None
        };
        target.SerialStopBits = parsed.GetValue(StopBits) switch {
            "one-point-five" => ToolkitSerialStopBits.OnePointFive,
            "two" => ToolkitSerialStopBits.Two,
            _ => ToolkitSerialStopBits.One
        };
        target.SerialHandshake = parsed.GetValue(Handshake) switch {
            "xon-xoff" => ToolkitSerialHandshake.XOnXOff,
            "rts-cts" => ToolkitSerialHandshake.RequestToSend,
            "rts-cts-xon-xoff" =>
                ToolkitSerialHandshake.RequestToSendXOnXOff,
            _ => ToolkitSerialHandshake.None
        };
        target.SerialDtrEnable = !parsed.GetValue(NoDtr);
        target.SerialRtsEnable = !parsed.GetValue(NoRts);
    }
}
