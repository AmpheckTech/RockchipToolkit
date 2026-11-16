using System.CommandLine;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support;
using Ampheck.RockchipToolkit.Arkt.Support.Adb;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Adb;

[CommandGroup(
    ["adb"],
    Description = "Access devices through the official ADB server.")]
internal sealed class AdbDeviceCommands(AdbCommandRunner runner) {
    [CommandHandler(
        ["adb", "devices"],
        Description = "List devices known to the official ADB server."
    )]
    private ValueTask<int> ExecuteDevicesAsync(
        [CommandParam] AdbCommandParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.ListDevicesAsync(
            parameters.SerialNumber,
            cancellationToken));

    [CommandHandler(
        ["adb", "push"],
        Description = "Copy a local file to the device."
    )]
    private ValueTask<int> ExecutePushAsync(
        [CommandParam(["source"], Position = 0)]
        string source,
        [CommandParam(["destination"], Position = 1)]
        string destination,
        [CommandParam] AdbCommandParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.PushAsync(
            parameters.SerialNumber,
            source,
            destination,
            cancellationToken));

    [CommandHandler(
        ["adb", "pull"],
        Description = "Copy a device file to the local machine."
    )]
    private ValueTask<int> ExecutePullAsync(
        [CommandParam(["source"], Position = 0)]
        string source,
        [CommandParam(["destination"], Position = 1)]
        string destination,
        [CommandParam] AdbCommandParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.PullAsync(
            parameters.SerialNumber,
            source,
            destination,
            cancellationToken));

    [CommandHandler(
        ["adb", "shell"],
        Description = "Start an interactive shell or run a command.",
        TreatUnmatchedTokensAsErrors = false)]
    private ValueTask<int> ExecuteShellAsync(
        [CommandParam(
            ["--command", "-c"],
            Description = "Run a shell command instead of an " +
                "interactive shell. Use -c for pipelines and loops.")]
        string? commandOption,
        [CommandParam(
            ["command"],
            Position = 0,
            MinimumArity = 0,
            MaximumArity = int.MaxValue,
            Description = "Command and arguments to run on the device.")]
        string[] command,
        [CommandParam] AdbCommandParameters parameters,
        ParseResult parsed,
        CommandInvocationContext invocation,
        CancellationToken cancellationToken
    ) {
        var arguments = command;
        if (arguments.Length > 0 &&
            parsed.UnmatchedTokens.Count > 0) {
            arguments = [
                .. arguments,
                .. parsed.UnmatchedTokens
            ];
        }

        var separator = Array.IndexOf(invocation.Arguments, "--");
        if (separator >= 0 &&
            commandOption is null) {
            arguments = invocation.Arguments
                .Skip(separator + 1)
                .ToArray();
        }

        var commandText = commandOption ??
            (arguments.Length == 0
                ? null
                : AdbShellCommandBuilder.FromArguments(arguments));
        return new ValueTask<int>(
            runner.ShellAsync(
                new AdbShellRequest(
                    parameters.SerialNumber,
                    commandText),
                cancellationToken));
    }
}
