using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class ServerCommands(CommandParseState state) {
    public void Register(RootCommand root) {
        var server = new Command(
            CommandNames.Server,
            "Run the optional toolkit server.");
        var nodaemon = new Command(
            "nodaemon",
            "Run the toolkit server in the foreground.");
        var listen = new Option<string>(
            "--listen") {
            Description = "Listen address as host:port.",
            DefaultValueFactory = _ =>
                ToolkitServerDefaults.Endpoint
        };
        var token = new Option<string?>(
            "--token") {
            Description = "Optional access token for RPC clients."
        };
        var hostName = new Option<string>(
            "--hostname") {
            Description = "Server name used by LAN discovery.",
            DefaultValueFactory = _ => Environment.MachineName
        };
        var idleTimeout = new Option<int>(
            "--idle-timeout") {
            Description = "Exit after this many idle seconds; 0 disables.",
            DefaultValueFactory = _ => 0
        };
        var verbose = new Option<bool>("--verbose", "-v") {
            Description = "Enable informational and debug logs."
        };
        var maxBlob = new Option<int>(
            "--max-blob-mib") {
            Description = "Maximum MiB per uploaded blob.",
            DefaultValueFactory = _ => 32 * 1024
        };
        var serialHistory = new Option<int>(
            "--serial-history-kib") {
            Description = "Serial history ring size in KiB.",
            DefaultValueFactory = _ => 256
        };
        nodaemon.Add(listen);
        nodaemon.Add(token);
        nodaemon.Add(hostName);
        nodaemon.Add(idleTimeout);
        nodaemon.Add(verbose);
        nodaemon.Add(maxBlob);
        nodaemon.Add(serialHistory);
        nodaemon.SetAction(parsed => {
            var options = new ServerNodaemonArguments(
                parsed.GetValue(listen) ??
                    ToolkitServerDefaults.Endpoint,
                parsed.GetValue(token),
                parsed.GetValue(hostName),
                parsed.GetValue(idleTimeout),
                parsed.GetValue(maxBlob),
                parsed.GetValue(serialHistory));
            state.Invocation = new CliInvocation(
                CommandNames.ServerNodaemon,
                RequiresServer: false,
                (context, _) => ServerCommandRunner.RunNodaemonAsync(
                    options,
                    context));
            return 0;
        });
        server.Add(nodaemon);
        server.Add(CreateControlCommand(
            "start",
            CommandNames.ServerStart,
            "Start the toolkit server in the background."));
        server.Add(CreateControlCommand(
            "stop",
            CommandNames.ServerStop,
            "Stop the background toolkit server."));
        server.Add(CreateControlCommand(
            "status",
            CommandNames.ServerStatus,
            "Show background toolkit server status."));
        root.Add(server);
        root.Add(CreateDiscoverCommand());
    }

    private Command CreateControlCommand(
        string name,
        string canonicalCommand,
        string description) {
        var command = new Command(name, description);
        command.SetAction(_ => {
            state.Invocation = new CliInvocation(
                canonicalCommand,
                RequiresServer: false,
                (context, _) => canonicalCommand switch {
                    CommandNames.ServerStart =>
                        ServerCommandRunner.StartAsync(context),
                    CommandNames.ServerStop =>
                        ServerCommandRunner.StopAsync(context),
                    _ => ServerCommandRunner.StatusAsync(context)
                });
            return 0;
        });
        return command;
    }

    private Command CreateDiscoverCommand() {
        var timeout = new Option<int>("--timeout") {
            Description = "Discovery wait time in seconds.",
            DefaultValueFactory = _ => 2
        };
        var command = new Command(
            CommandNames.Discover,
            "Find toolkit servers on the local network.");
        command.Add(timeout);
        command.SetAction(parsed => {
            var seconds = parsed.GetValue(timeout);
            state.Invocation = new CliInvocation(
                CommandNames.Discover,
                RequiresServer: false,
                (context, _) => ServerCommandRunner.DiscoverAsync(
                    seconds,
                    context));
            return 0;
        });
        return command;
    }
}
