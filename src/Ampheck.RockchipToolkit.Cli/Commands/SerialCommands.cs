using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class SerialCommands(CommandParseState state) {
    public void Register(RootCommand root) {
        var serial = new Command(
            CommandNames.Serial,
            "List and access serial ports.");
        serial.Add(CreateListCommand());
        serial.Add(CreateOpenCommand());
        serial.Add(CreateCloseCommand());
        serial.Add(CreateClearCommand());
        serial.Add(CreateTerminalCommand());
        serial.Add(CreateHistoryCommand());
        root.Add(serial);
    }

    private Command CreateListCommand() {
        var command = new Command(
            "list",
            "List local or server serial ports.");
        AddVerbose(command);
        command.SetAction(_ => {
            state.Invocation = new CliInvocation(
                CommandNames.SerialList,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new SerialCommandRunner(context).RunListAsync(
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateOpenCommand() {
        var command = new Command(
            "open",
            "Open and retain a serial port.");
        var port = PortArgument();
        var serialOptions = new SerialOptionSet();
        AddVerbose(command);
        command.Add(port);
        serialOptions.AddTo(command);
        command.SetAction(parsed => {
            var options = serialOptions.Parse(
                parsed,
                parsed.GetValue(port) ??
                throw new ArgumentException(
                    "A serial port is required."));
            state.Invocation = new CliInvocation(
                CommandNames.SerialOpen,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new SerialCommandRunner(context).RunOpenAsync(
                        options,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateCloseCommand() {
        var command = new Command(
            "close",
            "Close a retained serial port.");
        var port = PortArgument();
        AddVerbose(command);
        command.Add(port);
        command.SetAction(parsed => {
            SetPortInvocation(
                CommandNames.SerialClose,
                parsed.GetValue(port),
                (context, options, cancellationToken) =>
                    new SerialCommandRunner(context).RunCloseAsync(
                        options,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateClearCommand() {
        var command = new Command(
            "clear",
            "Clear retained output for an open serial port.");
        var port = PortArgument();
        AddVerbose(command);
        command.Add(port);
        command.SetAction(parsed => {
            SetPortInvocation(
                CommandNames.SerialClear,
                parsed.GetValue(port),
                (context, options, cancellationToken) =>
                    new SerialCommandRunner(context).RunClearAsync(
                        options,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateHistoryCommand() {
        var command = new Command(
            "history",
            "Print buffered serial output.");
        var port = PortArgument();
        var bytes = new Option<int>("--bytes") {
            Description = "Maximum bytes to print.",
            DefaultValueFactory = _ => 256 * 1024
        };
        AddVerbose(command);
        command.Add(port);
        command.Add(bytes);
        command.SetAction(parsed => {
            var maximumBytes = parsed.GetValue(bytes);
            if (maximumBytes is not > 0) {
                throw new ArgumentException(
                    "--bytes must be greater than zero.");
            }

            var options = new SerialHistoryArguments(
                parsed.GetValue(port) ??
                    throw new ArgumentException(
                        "A serial port is required."),
                maximumBytes);
            state.Invocation = new CliInvocation(
                CommandNames.SerialHistory,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new SerialCommandRunner(context).RunHistoryAsync(
                        options,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateTerminalCommand() {
        var command = new Command(
            "terminal",
            "Open an interactive serial terminal.");
        var port = PortArgument();
        var bytes = new Option<int>("--history-bytes") {
            Description = "Maximum retained bytes to replay.",
            DefaultValueFactory = _ => 256 * 1024
        };
        AddVerbose(command);
        command.Add(port);
        command.Add(bytes);
        command.SetAction(parsed => {
            var options = new SerialTerminalArguments(
                parsed.GetValue(port) ??
                    throw new ArgumentException(
                        "A serial port is required."),
                parsed.GetValue(bytes));
            state.Invocation = new CliInvocation(
                CommandNames.SerialTerminal,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new SerialCommandRunner(context).RunTerminalAsync(
                        options,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private void SetPortInvocation(
        string command,
        string? portName,
        Func<
            CliCommandContext,
            SerialPortArguments,
            CancellationToken,
            Task<int>> execute) {
        var options = new SerialPortArguments(
            portName ??
            throw new ArgumentException(
                "A serial port is required."));
        state.Invocation = new CliInvocation(
            command,
            RequiresServer: true,
            (context, cancellationToken) => execute(
                context,
                options,
                cancellationToken));
    }

    private void AddVerbose(Command command) {
        command.Add(new Option<bool>("--verbose", "-v") {
            Description = "Enable informational and debug logging."
        });
    }

    private Argument<string> PortArgument() {
        return new Argument<string>("port") {
            Description =
                "Serial port, for example COM17 or /dev/ttyUSB0."
        };
    }
}
