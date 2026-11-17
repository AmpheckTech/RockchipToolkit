using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Commands.Parsers;
using Ampheck.RockchipToolkit.Transport.Serial;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class SerialMonitorOptions {
    public Option<int> BaudRate { get; } = new("--baudrate", "--baud", "-b") {
        Description = "Baud rate.",
        DefaultValueFactory = _ => SerialPortDefaults.BaudRate
    };
    public Option<int> DataBits { get; } = new("--data-bits") {
        Description = "Data bits: 5, 6, 7, or 8.",
        DefaultValueFactory = _ => 8
    };
    public Option<string> Parity { get; } = new("--parity") {
        Description = "none, odd, even, mark, or space.",
        DefaultValueFactory = _ => "none"
    };
    public Option<string> StopBits { get; } = new("--stop-bits") {
        Description = "one, one-point-five, or two.",
        DefaultValueFactory = _ => "one"
    };
    public Option<string> Handshake { get; } = new("--handshake") {
        Description = "none, xon-xoff, rts-cts, or rts-cts-xon-xoff.",
        DefaultValueFactory = _ => "none"
    };
    public Option<bool> NoDtr { get; } = new("--no-dtr") {
        Description = "Leave DTR disabled."
    };
    public Option<bool> NoRts { get; } = new("--no-rts") {
        Description = "Leave RTS disabled."
    };

    public SerialMonitorOptions() {
        DataBits.CustomParser = result => ValueParser.ParseDataBits(
            "--data-bits",
            result.Tokens.Single().Value);
    }

    public void AddTo(Command command) {
        command.Add(BaudRate);
        command.Add(DataBits);
        command.Add(Parity);
        command.Add(StopBits);
        command.Add(Handshake);
        command.Add(NoDtr);
        command.Add(NoRts);
    }

    public SerialPortOptions Read(string portName, ParseResult parsed) => new() {
        PortName = portName,
        BaudRate = parsed.GetValue(BaudRate),
        DataBits = parsed.GetValue(DataBits),
        Parity = ValueParser.ParseParity(
            "--parity",
            parsed.GetValue(Parity)!),
        StopBits = ValueParser.ParseStopBits(
            "--stop-bits",
            parsed.GetValue(StopBits)!),
        Handshake = ValueParser.ParseHandshake(
            "--handshake",
            parsed.GetValue(Handshake)!),
        DtrEnable = !parsed.GetValue(NoDtr),
        RtsEnable = !parsed.GetValue(NoRts)
    };
}
