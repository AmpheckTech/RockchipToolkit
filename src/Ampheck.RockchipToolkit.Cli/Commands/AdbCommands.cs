using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class AdbCommands(CommandParseState state) {
    public void Register(RootCommand root) {
        var command = new Command(
            CommandNames.Adb,
            "Access devices through the official ADB server.");
        command.Add(CreateDevicesCommand());
        command.Add(CreateShellCommand());
        command.Add(CreateTransferCommand(
            "push",
            "Copy a local file to the device."));
        command.Add(CreateTransferCommand(
            "pull",
            "Copy a device file to the local machine."));
        command.Add(CreateRebootCommand());
        root.Add(command);
    }

    private Command CreateDevicesCommand() {
        var command = new Command(
            "devices",
            "List devices known to the official ADB server.");
        var options = new AdbOptionSet();
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            state.Invocation = new CliInvocation(
                CommandNames.AdbDevices,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new AdbCommandRunner(context).ListDevicesAsync(
                        new AdbDeviceArguments(
                            values.SerialNumber,
                            values.LoaderTarget),
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateShellCommand() {
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
        var options = new AdbOptionSet();
        command.Add(commandArgument);
        command.Add(commandOption);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var arguments = parsed.GetValue(commandArgument) ?? [];
            if (arguments.Length > 0 &&
                parsed.UnmatchedTokens.Count > 0) {
                arguments = [
                    .. arguments,
                    .. parsed.UnmatchedTokens
                ];
            }

            var separator = Array.IndexOf(
                state.Arguments,
                "--");
            if (separator >= 0 &&
                parsed.GetValue(commandOption) is null) {
                arguments = state.Arguments[(separator + 1)..];
            }

            var shell = new AdbShellArguments(
                values.SerialNumber,
                parsed.GetValue(commandOption),
                arguments);
            state.Invocation = new CliInvocation(
                CommandNames.AdbShell,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new AdbCommandRunner(context).ShellAsync(
                        shell,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateTransferCommand(
        string name,
        string description) {
        var command = new Command(name, description);
        var source = new Argument<string>("source");
        var destination = new Argument<string>("destination");
        var options = new AdbOptionSet();
        command.Add(source);
        command.Add(destination);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var transfer = new AdbTransferArguments(
                values.SerialNumber,
                parsed.GetValue(source) ??
                    throw new ArgumentException(
                        "A source path is required."),
                parsed.GetValue(destination) ??
                    throw new ArgumentException(
                        "A destination path is required."));
            state.Invocation = new CliInvocation(
                name == "push"
                    ? CommandNames.AdbPush
                    : CommandNames.AdbPull,
                RequiresServer: true,
                (context, cancellationToken) => {
                    var runner = new AdbCommandRunner(context);
                    return name == "push"
                        ? runner.PushAsync(
                            transfer,
                            cancellationToken)
                        : runner.PullAsync(
                            transfer,
                            cancellationToken);
                });
            return 0;
        });
        return command;
    }

    private Command CreateRebootCommand() {
        var reboot = new Command(
            "reboot",
            "Reboot an ADB device. Defaults to normal mode.");
        AddRebootAction(
            reboot,
            CommandNames.AdbRebootSystem,
            ToolkitAdbRebootMode.System,
            includeLoaderFilters: false);

        var normal = new Command(
            "normal",
            "Reboot into the system.");
        normal.Aliases.Add("system");
        AddRebootAction(
            normal,
            CommandNames.AdbRebootSystem,
            ToolkitAdbRebootMode.System,
            includeLoaderFilters: false);

        var recovery = new Command(
            "recovery",
            "Reboot into recovery mode.");
        AddRebootAction(
            recovery,
            CommandNames.AdbRebootRecovery,
            ToolkitAdbRebootMode.Recovery,
            includeLoaderFilters: false);

        var loader = new Command(
            "loader",
            "Reboot into RockUSB Loader mode.");
        AddRebootAction(
            loader,
            CommandNames.AdbRebootLoader,
            ToolkitAdbRebootMode.Bootloader,
            includeLoaderFilters: true);

        reboot.Add(normal);
        reboot.Add(recovery);
        reboot.Add(loader);
        return reboot;
    }

    private void AddRebootAction(
        Command command,
        string canonicalCommand,
        ToolkitAdbRebootMode mode,
        bool includeLoaderFilters) {
        var options = new AdbOptionSet(includeLoaderFilters);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var reboot = new AdbRebootArguments(
                values.SerialNumber,
                mode,
                values.LoaderTarget);
            state.Invocation = new CliInvocation(
                canonicalCommand,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new AdbCommandRunner(context).RebootAsync(
                        reboot,
                        cancellationToken));
            return 0;
        });
    }
}
