using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class AdbCommands {
    public static Command Create(CommandParseState state) {
        var command = new Command(
            CommandNames.Adb,
            "Access a device through its ADB USB interface.");
        command.Add(CreateDevicesCommand(state));
        command.Add(CreateShellCommand(state));
        command.Add(CreateTransferCommand(
            state,
            "push",
            CommandNames.AdbPush,
            "Copy a local file to the device."));
        command.Add(CreateTransferCommand(
            state,
            "pull",
            CommandNames.AdbPull,
            "Copy a device file to the local machine."));

        var reboot = new Command(
            "reboot",
            "Reboot an ADB device.");
        var rebootLoader = new Command(
            "loader",
            "Reboot into RockUSB Loader mode.");
        CommandOptionsBinder.AddAdbOptions(
            rebootLoader,
            state,
            CommandNames.AdbRebootLoader);
        reboot.Add(rebootLoader);
        command.Add(reboot);
        return command;
    }

    private static Command CreateDevicesCommand(
        CommandParseState state) {
        var command = new Command(
            "devices",
            "List ADB USB devices.");
        CommandOptionsBinder.AddAdbOptions(
            command,
            state,
            CommandNames.AdbDevices);
        return command;
    }

    private static Command CreateShellCommand(
        CommandParseState state) {
        var command = new Command(
            "shell",
            "Start an interactive shell or run a command.");
        command.TreatUnmatchedTokensAsErrors = false;
        var commandArgument = new Argument<string[]>(
            "command") {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Command and arguments to run on the device."
        };
        var commandOption = new Option<string?>(
            "--command",
            "-c") {
            Description = "Command to run instead of an interactive shell."
        };
        command.Add(commandArgument);
        command.Add(commandOption);
        CommandOptionsBinder.AddAdbOptions(
            command,
            state,
            CommandNames.AdbShell,
            bind: parsed => {
                var values =
                    parsed.GetValue(commandArgument) ?? [];
                if (values.Length > 0 &&
                    parsed.UnmatchedTokens.Count > 0) {
                    values = [
                        .. values,
                        .. parsed.UnmatchedTokens
                    ];
                }

                state.Options.AdbShellCommand =
                    parsed.GetValue(commandOption) ??
                    (values.Length == 0
                        ? null
                        : string.Join(" ", values));
            });
        return command;
    }

    private static Command CreateTransferCommand(
        CommandParseState state,
        string name,
        string canonicalCommand,
        string description) {
        var command = new Command(name, description);
        var source = new Argument<string>("source");
        var destination = new Argument<string>("destination");
        command.Add(source);
        command.Add(destination);
        CommandOptionsBinder.AddAdbOptions(
            command,
            state,
            canonicalCommand,
            bind: parsed => {
                state.Options.AdbSource =
                    parsed.GetValue(source);
                state.Options.AdbDestination =
                    parsed.GetValue(destination);
            });
        return command;
    }
}
