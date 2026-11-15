using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
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
        serial.Add(CreateClearCommand(state));
        serial.Add(CreateTerminalCommand(state));
        serial.Add(CreateHistoryCommand(state));
        return serial;
    }

    private static Command CreateListCommand(
        CommandParseState state) {
        var command = new Command(
            "list",
            "List local or server serial ports.");
        var options = new CommandOptions();
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            options,
            CommandNames.SerialList,
            (context, _) => ExecuteRpc(
                CommandNames.SerialList,
                options,
                context));
        return command;
    }

    private static Command CreateTerminalCommand(
        CommandParseState state) {
        var command = new Command(
            "terminal",
            "Open an interactive serial terminal.");
        var port = PortArgument();
        var options = new CommandOptions();
        command.Add(port);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            options,
            CommandNames.SerialTerminal,
            (context, _) => ExecuteRpc(
                CommandNames.SerialTerminal,
                options,
                context),
            bind: parsed => options.SerialPort =
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
        var options = new CommandOptions();
        command.Add(port);
        serialOptions.AddTo(command);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            options,
            CommandNames.SerialOpen,
            (context, _) => ExecuteRpc(
                CommandNames.SerialOpen,
                options,
                context),
            bind: parsed => serialOptions.Apply(
                options,
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
        var options = new CommandOptions();
        command.Add(port);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            options,
            CommandNames.SerialClose,
            (context, _) => ExecuteRpc(
                CommandNames.SerialClose,
                options,
                context),
            bind: parsed => options.SerialPort =
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
        var options = new CommandOptions();
        command.Add(bytes);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            options,
            CommandNames.SerialHistory,
            (context, _) => ExecuteRpc(
                CommandNames.SerialHistory,
                options,
                context),
            bind: parsed => {
                options.SerialPort =
                    parsed.GetValue(port) ??
                    throw new ArgumentException(
                        "A serial port is required.");
                options.SerialHistoryBytes =
                    parsed.GetValue(bytes);
                if (options.SerialHistoryBytes is not > 0) {
                    throw new ArgumentException(
                        "--bytes must be greater than zero.");
                }
            });
        return command;
    }

    private static Command CreateClearCommand(
        CommandParseState state) {
        var command = new Command(
            "clear",
            "Clear retained output for an open serial port.");
        var port = PortArgument();
        var options = new CommandOptions();
        command.Add(port);
        CommandOptionsBinder.AddSerialEndpointOptions(
            command,
            state,
            options,
            CommandNames.SerialClear,
            (context, _) => ExecuteRpc(
                CommandNames.SerialClear,
                options,
                context),
            bind: parsed => options.SerialPort =
                parsed.GetValue(port) ??
                throw new ArgumentException(
                    "A serial port is required."));
        return command;
    }

    private static Argument<string> PortArgument() {
        return new Argument<string>("port") {
            Description = "Serial port, for example COM17 or /dev/ttyUSB0."
        };
    }

    private static Task<int> ExecuteRpc(
        string command,
        CommandOptions options,
        CliCommandContext context) {
        return ToolkitClientCommandRunner.RunAsync(
            command,
            options,
            context.Endpoint!,
            context.Logger,
            context.Console);
    }
}
