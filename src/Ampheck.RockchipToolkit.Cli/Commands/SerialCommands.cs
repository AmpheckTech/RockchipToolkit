using System.CommandLine;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class SerialCommands {
    public static Command Create(
        CommandParseState state) {
        var serial = new Command(
            CommandNames.Serial,
            "List and access serial ports.");
        serial.Add(CreateListCommand(state));
        serial.Add(CreateOpenCommand(state));
        serial.Add(CreateCloseCommand(state));
        serial.Add(CreateTerminalCommand(state));
        serial.Add(CreateHistoryCommand(state));
        return serial;
    }

    private static Command CreateListCommand(
        CommandParseState state) {
        var command = new Command(
            "list",
            "List local or server serial ports.");
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            CommandNames.SerialList);
        return command;
    }

    private static Command CreateTerminalCommand(
        CommandParseState state) {
        var command = new Command(
            "terminal",
            "Open an interactive serial terminal.");
        var port = PortArgument();
        command.Add(port);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            CommandNames.SerialTerminal,
            bind: parsed => state.Options.SerialPort =
                parsed.GetValue(port) ??
                throw new ArgumentException(
                    "A serial port is required."));
        return command;
    }

    private static Command CreateOpenCommand(
        CommandParseState state) {
        var command = new Command(
            "open",
            "Open and retain a serial port.");
        var port = PortArgument();
        var serialOptions = new SerialOptionSet();
        command.Add(port);
        serialOptions.AddTo(command);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            CommandNames.SerialOpen,
            bind: parsed => serialOptions.Apply(
                state.Options,
                parsed,
                parsed.GetValue(port) ??
                throw new ArgumentException(
                    "A serial port is required.")));
        return command;
    }

    private static Command CreateCloseCommand(
        CommandParseState state) {
        var command = new Command(
            "close",
            "Close a retained serial port.");
        var port = PortArgument();
        command.Add(port);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            CommandNames.SerialClose,
            bind: parsed => state.Options.SerialPort =
                parsed.GetValue(port) ??
                throw new ArgumentException(
                    "A serial port is required."));
        return command;
    }

    private static Command CreateHistoryCommand(
        CommandParseState state) {
        var command = new Command(
            "history",
            "Print buffered serial output.");
        var port = PortArgument();
        command.Add(port);
        var bytes = new Option<int>("--bytes") {
            Description = "Maximum bytes to print.",
            DefaultValueFactory = _ => 256 * 1024
        };
        command.Add(bytes);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            CommandNames.SerialHistory,
            bind: parsed => {
                state.Options.SerialPort =
                    parsed.GetValue(port) ??
                    throw new ArgumentException(
                        "A serial port is required.");
                state.Options.SerialHistoryBytes =
                    parsed.GetValue(bytes);
                if (state.Options.SerialHistoryBytes is not > 0) {
                    throw new ArgumentException(
                        "--bytes must be greater than zero.");
                }
            });
        return command;
    }

    private static Argument<string> PortArgument() {
        return new Argument<string>("port") {
            Description = "Serial port, for example COM17 or /dev/ttyUSB0."
        };
    }

}
