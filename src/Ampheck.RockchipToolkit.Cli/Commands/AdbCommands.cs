using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class AdbCommands {
    public static Command Create(CommandParseState state) {
        var command = new Command(
            CommandNames.Adb,
            "Access devices through the official ADB server.");
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
            "Reboot an ADB device. Defaults to normal mode.");
        var rebootOptions = new CommandOptions();
        CommandOptionsBinder.AddAdbOptions(
            reboot,
            state,
            rebootOptions,
            CommandNames.AdbRebootSystem,
            (context, _) => ExecuteRpc(
                CommandNames.AdbRebootSystem,
                rebootOptions,
                context));
        var rebootNormal = new Command(
            "normal",
            "Reboot into the system.");
        rebootNormal.Aliases.Add("system");
        var normalOptions = new CommandOptions();
        CommandOptionsBinder.AddAdbOptions(
            rebootNormal,
            state,
            normalOptions,
            CommandNames.AdbRebootSystem,
            (context, _) => ExecuteRpc(
                CommandNames.AdbRebootSystem,
                normalOptions,
                context));
        var rebootRecovery = new Command(
            "recovery",
            "Reboot into recovery mode.");
        var recoveryOptions = new CommandOptions();
        CommandOptionsBinder.AddAdbOptions(
            rebootRecovery,
            state,
            recoveryOptions,
            CommandNames.AdbRebootRecovery,
            (context, _) => ExecuteRpc(
                CommandNames.AdbRebootRecovery,
                recoveryOptions,
                context));
        var rebootLoader = new Command(
            "loader",
            "Reboot into RockUSB Loader mode.");
        var loaderOptions = new CommandOptions();
        CommandOptionsBinder.AddAdbOptions(
            rebootLoader,
            state,
            loaderOptions,
            CommandNames.AdbRebootLoader,
            (context, _) => ExecuteRpc(
                CommandNames.AdbRebootLoader,
                loaderOptions,
                context),
            includeLoaderFilters: true);
        reboot.Add(rebootNormal);
        reboot.Add(rebootRecovery);
        reboot.Add(rebootLoader);
        command.Add(reboot);
        return command;
    }

    private static Command CreateDevicesCommand(
        CommandParseState state) {
        var command = new Command(
            "devices",
            "List devices known to the official ADB server.");
        var options = new CommandOptions();
        CommandOptionsBinder.AddAdbOptions(
            command,
            state,
            options,
            CommandNames.AdbDevices,
            (context, _) => ExecuteRpc(
                CommandNames.AdbDevices,
                options,
                context));
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
            Description = "Run a shell command instead of an " +
                "interactive shell. Use -c for pipelines and loops."
        };
        command.Add(commandArgument);
        command.Add(commandOption);
        var options = new CommandOptions();
        CommandOptionsBinder.AddAdbOptions(
            command,
            state,
            options,
            CommandNames.AdbShell,
            (context, _) => ExecuteRpc(
                CommandNames.AdbShell,
                options,
                context),
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

                options.AdbShellCommand =
                    parsed.GetValue(commandOption);
                options.AdbShellArguments =
                    options.AdbShellCommand is null
                        ? [.. values]
                        : [];
                var separator = Array.IndexOf(
                    state.Arguments,
                    "--");
                if (separator >= 0 &&
                    options.AdbShellCommand is null) {
                    options.AdbShellArguments =
                        state.Arguments[(separator + 1)..];
                }
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
        var options = new CommandOptions();
        command.Add(source);
        command.Add(destination);
        CommandOptionsBinder.AddAdbOptions(
            command,
            state,
            options,
            canonicalCommand,
            (context, _) => ExecuteRpc(
                canonicalCommand,
                options,
                context),
            bind: parsed => {
                options.AdbSource =
                    parsed.GetValue(source);
                options.AdbDestination =
                    parsed.GetValue(destination);
            });
        return command;
    }

    private static Task<int> ExecuteRpc(
        string command,
        CommandOptions options,
        CliCommandContext context) {
        return AdbCommandRunner.ExecuteAsync(
            command,
            options,
            context.Endpoint!,
            context.Logger,
            context.Console);
    }
}
