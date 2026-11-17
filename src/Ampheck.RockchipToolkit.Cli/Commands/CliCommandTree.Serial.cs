using System.CommandLine;
using System.Text;
using Ampheck.RockchipToolkit.Cli.Terminal;
using Ampheck.RockchipToolkit.Hardware.Serial;
using Ampheck.RockchipToolkit.Transport.Serial;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private static readonly TimeSpan SerialConnectTimeout =
        TimeSpan.FromSeconds(5);

    private Command BuildSerial() {
        var group = new Command(
            "serial",
            "List and monitor serial ports.") {
            BuildSerialList(),
            BuildSerialMonitor(),
            BuildSerialSend()
        };
        return group;
    }

    private Command BuildSerialList() {
        var command = new Command("list", "List serial ports.");
        command.SetAction((_, _) => {
            foreach (var device in SerialPortDiscovery.List()) {
                Console.WriteLine(device);
            }

            return Task.FromResult(0);
        });
        return command;
    }

    private Command BuildSerialMonitor() {
        var port = CreateArgument<string>(
            "port",
            "Serial port, for example COM17 or /dev/ttyUSB0.");
        var options = new SerialMonitorOptions();
        var command = new Command("monitor", "Monitor a serial port.") {
            port
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => MonitorSerialAsync(
            options.Read(parsed.GetValue(port)!, parsed),
            token));
        return command;
    }

    private Command BuildSerialSend() {
        var port = CreateArgument<string>(
            "port",
            "Serial port, for example COM17 or /dev/ttyUSB0.");
        var text = CreateArgument<string>("text", "Text to send.");
        var command = new Command(
            "send",
            "Send UTF-8 text to a serial port.") {
            port,
            text
        };
        command.SetAction((parsed, token) => SendSerialAsync(
            parsed.GetValue(port)!,
            parsed.GetValue(text)!,
            token));
        return command;
    }

    private async Task<int> MonitorSerialAsync(
        SerialPortOptions options,
        CancellationToken cancellationToken
    ) {
        await using var serial = new SerialHubManager();
        await using var session = await serial.OpenSessionAsync(options)
            .ConfigureAwait(false);
        await session.WaitForConnectionAsync(
                SerialConnectTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var output = PumpSerialOutputAsync(session, cancellationToken);
        await InteractiveTerminal.RunAsync(
                (data, token) => session.WriteAsync(data, token),
                output,
                _console,
                "Serial monitor. Use Ctrl+] to leave.",
                cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    private async Task<int> SendSerialAsync(
        string portName,
        string text,
        CancellationToken cancellationToken
    ) {
        await using var serial = new SerialHubManager();
        await using var session = await serial.OpenSessionAsync(
                new SerialPortOptions { PortName = portName })
            .ConfigureAwait(false);
        await session.WaitForConnectionAsync(
                SerialConnectTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        await session.WriteAsync(
                Encoding.UTF8.GetBytes(text),
                cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    private static async Task PumpSerialOutputAsync(
        ISerialSession session,
        CancellationToken cancellationToken
    ) {
        await foreach (var value in session.Output.ReadAllAsync(
            cancellationToken)) {
            await InteractiveTerminal.WriteOutputAsync(
                    value,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
