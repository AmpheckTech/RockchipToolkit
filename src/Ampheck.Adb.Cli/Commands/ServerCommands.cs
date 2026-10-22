using System.CommandLine;
using Ampheck.Adb.Server;

namespace Ampheck.Adb.Cli.Commands;

internal static class ServerCommands {
    public static void AddTo(
        RootCommand root,
        AdbCliOptions options) {
        root.Add(CreateDevices(options));
        root.Add(CreateStartServer(options));
        root.Add(CreateKillServer(options));
        root.Add(CreateServerCommand(options));
        root.Add(CreateVersion());
        root.Add(CreateConnect(options));
        root.Add(CreateDisconnect(options));
    }

    private static Command CreateDevices(AdbCliOptions options) {
        var detailed = new Option<bool>("--long", "-l") {
            Description = "Show device details."
        };
        var command = new Command(
            "devices",
            "List connected Android devices.");
        command.Add(detailed);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var devices = await client.GetDevicesAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                foreach (var device in devices) {
                    Console.Write(
                        $"{device.SerialNumber}\t{FormatState(device.State)}");
                    if (parseResult.GetValue(detailed)) {
                        Console.Write(
                            $"\tproduct:{device.Product ?? "unknown"}" +
                            $" model:{device.Model ?? "unknown"}" +
                            $" device:{device.DeviceName ?? "unknown"}");
                    }

                    Console.WriteLine();
                }
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateStartServer(AdbCliOptions options) {
        var command = new Command(
            "start-server",
            "Start the ADB server if it is not already running.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                await AdbServerLauncher.EnsureRunningAsync(
                        options.CreateServerOptions(parseResult),
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateKillServer(AdbCliOptions options) {
        var command = new Command(
            "kill-server",
            "Stop the ADB server.");
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                await AdbServerClient.KillAsync(
                        cancellationToken,
                        options.CreateServerOptions(parseResult))
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateServerCommand(AdbCliOptions options) {
        var command = new Command(
            "server",
            "Run or manage the ADB server.");
        var nodaemon = new Command(
            "nodaemon",
            "Run the ADB server in the foreground.");
        nodaemon.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var serverOptions = options.CreateServerOptions(parseResult);
                await using var daemon = new AdbServerDaemon(
                    new AdbServerDaemonOptions {
                        Host = serverOptions.Host,
                        Port = serverOptions.Port
                    });
                using var stop = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken);
                ConsoleCancelEventHandler handler = (_, eventArgs) => {
                    eventArgs.Cancel = true;
                    stop.Cancel();
                };
                Console.CancelKeyPress += handler;
                try {
                    await daemon.RunAsync(stop.Token)
                        .ConfigureAwait(false);
                } finally {
                    Console.CancelKeyPress -= handler;
                }
            }).ConfigureAwait(false));
        command.Add(nodaemon);
        return command;
    }

    private static Command CreateVersion() {
        var command = new Command(
            "version",
            "Show the adbcli version.");
        command.SetAction(_ => {
            Console.WriteLine(
                $"Ampheck ADB CLI {AdbCliVersion.Current}");
        });
        return command;
    }

    private static Command CreateConnect(AdbCliOptions options) {
        var address = new Argument<string>("address");
        var command = new Command(
            "connect",
            "Connect to a network ADB device.");
        command.Add(address);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var (host, port) = ParseAddress(
                    parseResult.GetValue(address)!);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var result = await client.ConnectNetworkAsync(
                        host,
                        port,
                        cancellationToken)
                    .ConfigureAwait(false);
                Console.WriteLine(result);
            }).ConfigureAwait(false));
        return command;
    }

    private static Command CreateDisconnect(AdbCliOptions options) {
        var address = new Argument<string?>(
            "address") {
            Arity = ArgumentArity.ZeroOrOne
        };
        var command = new Command(
            "disconnect",
            "Disconnect a network ADB device.");
        command.Add(address);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                var value = parseResult.GetValue(address);
                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var result = string.IsNullOrWhiteSpace(value)
                    ? await client.DisconnectAllNetworkAsync(
                            cancellationToken)
                        .ConfigureAwait(false)
                    : await DisconnectAddressAsync(
                            client,
                            value,
                            cancellationToken)
                        .ConfigureAwait(false);
                Console.WriteLine(result);
            }).ConfigureAwait(false));
        return command;
    }

    private static Task<string> DisconnectAddressAsync(
        AdbClient client,
        string value,
        CancellationToken cancellationToken) {
        var (host, port) = ParseAddress(value);
        return client.DisconnectNetworkAsync(
            host,
            port,
            cancellationToken);
    }

    private static (string Host, int Port) ParseAddress(string value) {
        var separator = value.LastIndexOf(':');
        if (separator <= 0) {
            return (value, 5555);
        }

        if (!int.TryParse(
                value[(separator + 1)..],
                out var port) ||
            port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentException(
                $"Invalid ADB address '{value}'.");
        }

        return (value[..separator], port);
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
