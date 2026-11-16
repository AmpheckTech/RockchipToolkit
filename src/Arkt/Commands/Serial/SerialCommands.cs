using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support.Serial;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial;

[CommandGroup(
    ["serial"],
    Description = "List and monitor serial ports.")]
internal sealed class SerialCommands(SerialCommandRunner runner) {
    [CommandHandler(
        ["serial", "list"],
        Description = "List serial ports.")]
    private ValueTask<int> ExecuteListAsync(
        CancellationToken cancellationToken
    ) => new(runner.ListAsync());

    [CommandHandler(
        ["serial", "monitor"],
        Description = "Monitor a serial port.")]
    private ValueTask<int> ExecuteMonitorAsync(
        [CommandParam(
            ["port"],
            Position = 0,
            Description =
                "Serial port, for example COM17 or /dev/ttyUSB0.")]
        string portName,
        [CommandParam] SerialOpenParameters parameters,
        CancellationToken cancellationToken
    ) {
        if (parameters.BaudRate is not > 0) {
            throw new ArgumentException(
                "--baudrate must be greater than zero.");
        }

        return new ValueTask<int>(
            runner.MonitorAsync(
                portName,
                new SerialPortOptions {
                    PortName = portName,
                    BaudRate = parameters.BaudRate,
                    DataBits = parameters.DataBits,
                    Parity = parameters.Parity,
                    StopBits = parameters.StopBits,
                    Handshake = parameters.Handshake,
                    DtrEnable = !parameters.NoDtr,
                    RtsEnable = !parameters.NoRts
                },
                cancellationToken));
    }

    [CommandHandler(
        ["serial", "send"],
        Description = "Send UTF-8 text to a serial port.")]
    private ValueTask<int> ExecuteSendAsync(
        [CommandParam(
            ["port"],
            Position = 0,
            Description =
                "Serial port, for example COM17 or /dev/ttyUSB0.")]
        string portName,
        [CommandParam(
            ["text"],
            Position = 1,
            Description = "Text to send.")]
        string text,
        CancellationToken cancellationToken
    ) => new(runner.SendAsync(
        portName,
        text,
        cancellationToken));
}
