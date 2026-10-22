using System.CommandLine;
using Ampheck.Adb.Server;

namespace Ampheck.Adb.Cli.Commands;

internal static class DeviceCommands {
    public static void AddTo(
        RootCommand root,
        AdbCliOptions options) {
        root.Add(CreateShell(options));
        root.Add(CreateExecOut(options));
        root.Add(CreateWaitForDevice(options));
        root.Add(CreateGetState(options));
        root.Add(CreateGetSerialNumber(options));
        root.Add(CreateReboot(options));
        root.Add(CreateLogcat(options));
        root.Add(CreateServiceCommand(
            options,
            "root",
            "Restart adbd with root permissions.",
            "root:"));
        root.Add(CreateServiceCommand(
            options,
            "remount",
            "Remount the device filesystem read-write.",
            "remount:"));
        root.Add(CreateTcpip(options));
        root.Add(CreateUsb(options));
    }

    private static Command CreateShell(AdbCliOptions options) {
        var (command, arguments) = CreateShellLikeCommand(
            "shell",
            "Open an interactive shell or run a command.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var shellCommand = ReadShellCommand(
                    parseResult,
                    arguments);
                if (shellCommand is null) {
                    await using var shell = await device.OpenShellAsync(
                            cancellationToken)
                        .ConfigureAwait(false);
                    await InteractiveTerminal.RunAsync(
                            shell,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                var output = await device.ExecuteShellAsync(
                        shellCommand,
                        cancellationToken)
                    .ConfigureAwait(false);
                Console.Out.Write(output);
                Console.Out.Flush();
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateExecOut(AdbCliOptions options) {
        var (command, arguments) = CreateShellLikeCommand(
            "exec-out",
            "Run a device command and return its raw output.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var shellCommand = ReadShellCommand(
                        parseResult,
                        arguments)
                    ?? throw new ArgumentException(
                        "A command is required.");
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var output = await device.ExecuteShellAsync(
                        shellCommand,
                        cancellationToken)
                    .ConfigureAwait(false);
                Console.Out.Write(output);
                Console.Out.Flush();
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateWaitForDevice(AdbCliOptions options) {
        var timeout = new Option<int?>("--timeout") {
            Description = "Maximum wait time in seconds."
        };
        var command = new Command(
            "wait-for-device",
            "Wait for an online ADB device.");
        command.Add(timeout);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var seconds = parseResult.GetValue(timeout);
                await client.WaitForDeviceAsync(
                        settings.SerialNumber,
                        seconds is null
                            ? null
                            : TimeSpan.FromSeconds(seconds.Value),
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateGetState(AdbCliOptions options) {
        var command = new Command(
            "get-state",
            "Print the selected device state.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await client.GetDeviceAsync(
                        settings.SerialNumber,
                        cancellationToken)
                    .ConfigureAwait(false);
                Console.WriteLine(FormatState(device.State));
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateGetSerialNumber(AdbCliOptions options) {
        var command = new Command(
            "get-serialno",
            "Print the selected device serial number.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await client.GetDeviceAsync(
                        settings.SerialNumber,
                        cancellationToken)
                    .ConfigureAwait(false);
                Console.WriteLine(device.SerialNumber);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateReboot(AdbCliOptions options) {
        var mode = new Argument<string?>(
            "mode") {
            Arity = ArgumentArity.ZeroOrOne
        };
        var command = new Command(
            "reboot",
            "Reboot the selected device.");
        command.Add(mode);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                await device.RebootAsync(
                        ParseRebootMode(parseResult.GetValue(mode)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateLogcat(AdbCliOptions options) {
        var arguments = new Argument<string[]>(
            "arguments") {
            Arity = ArgumentArity.ZeroOrMore
        };
        var command = new Command(
            "logcat",
            "Print device logs.");
        command.TreatUnmatchedTokensAsErrors = false;
        command.Add(arguments);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var service = BuildShellService(
                    "logcat",
                    parseResult,
                    arguments);
                await RunServerServiceAsync(
                        parseResult,
                        options,
                        service,
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateServiceCommand(
        AdbCliOptions options,
        string name,
        string description,
        string service) {
        var command = new Command(name, description);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                await RunServerServiceAsync(
                        parseResult,
                        options,
                        service,
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateTcpip(AdbCliOptions options) {
        var port = new Argument<int>("port") {
            DefaultValueFactory = _ => 5555
        };
        var command = new Command(
            "tcpip",
            "Restart adbd in TCP mode.");
        command.Add(port);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                await RunServerServiceAsync(
                        parseResult,
                        options,
                        $"tcpip:{parseResult.GetValue(port)}",
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateUsb(AdbCliOptions options) {
        var command = new Command(
            "usb",
            "Restart adbd in USB mode.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                await RunServerServiceAsync(
                        parseResult,
                        options,
                        "usb:",
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static (Command Command, Argument<string[]> Arguments)
        CreateShellLikeCommand(
        string name,
        string description) {
        var arguments = new Argument<string[]>(
            "command") {
            Arity = ArgumentArity.ZeroOrMore
        };
        var command = new Command(name, description);
        command.TreatUnmatchedTokensAsErrors = false;
        command.Add(arguments);
        return (command, arguments);
    }

    private static string? ReadShellCommand(
        ParseResult parseResult,
        Argument<string[]> arguments) {
        var values = parseResult.GetValue(arguments) ?? [];
        if (values.Length > 0 &&
            parseResult.UnmatchedTokens.Count > 0) {
            values = [
                .. values,
                .. parseResult.UnmatchedTokens
            ];
        }

        return values.Length == 0
            ? null
            : string.Join(" ", values);
    }

    private static string BuildShellService(
        string command,
        ParseResult parseResult,
        Argument<string[]> arguments) {
        var commandLine = ReadShellCommand(parseResult, arguments);
        return string.IsNullOrWhiteSpace(commandLine)
            ? $"shell:{command}"
            : $"shell:{command} {commandLine}";
    }

    private static async Task RunServerServiceAsync(
        ParseResult parseResult,
        AdbCliOptions options,
        string service,
        CancellationToken cancellationToken) {
        var settings = options.Read(parseResult);
        if (settings.Direct) {
            throw new InvalidOperationException(
                "This service requires an ADB server.");
        }

        var client = await AdbClientFactory.CreateAsync(
                settings,
                cancellationToken)
            .ConfigureAwait(false);
        var device = await DeviceSelection.RequireOnlineAsync(
                client,
                settings,
                cancellationToken)
            .ConfigureAwait(false);
        await using var stream = await AdbServerClient.OpenServiceAsync(
                device.SerialNumber,
                service,
                cancellationToken,
                new AdbServerOptions {
                    Host = settings.Host,
                    Port = settings.Port
                })
            .ConfigureAwait(false);
        await stream.CopyToAsync(
                Console.OpenStandardOutput(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static AdbRebootMode ParseRebootMode(string? value) {
        return value?.ToLowerInvariant() switch {
            null or "" or "system" => AdbRebootMode.System,
            "bootloader" or "loader" => AdbRebootMode.Bootloader,
            "recovery" => AdbRebootMode.Recovery,
            "sideload" => AdbRebootMode.Sideload,
            _ => throw new ArgumentException(
                $"Unknown reboot mode '{value}'.")
        };
    }

    private static string FormatState(AdbDeviceState state) {
        return state switch {
            AdbDeviceState.Device => "device",
            AdbDeviceState.Offline => "offline",
            AdbDeviceState.Unauthorized => "unauthorized",
            AdbDeviceState.Recovery => "recovery",
            AdbDeviceState.Sideload => "sideload",
            AdbDeviceState.Bootloader => "bootloader",
            _ => "unknown"
        };
    }
}
