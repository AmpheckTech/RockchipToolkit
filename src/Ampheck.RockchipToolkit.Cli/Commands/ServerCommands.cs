using System.CommandLine;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class ServerCommands {
    public static void AddTo(
        RootCommand root,
        CommandParseState state) {
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
        var maxAttachment = new Option<int>(
            "--max-attachment-mib") {
            Description = "Maximum compressed MiB per attachment.",
            DefaultValueFactory = _ => 4096
        };
        var maxRequest = new Option<int>(
            "--max-request-mib") {
            Description = "Maximum compressed MiB per request.",
            DefaultValueFactory = _ => 8192
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
        nodaemon.Add(maxAttachment);
        nodaemon.Add(maxRequest);
        nodaemon.Add(serialHistory);
        nodaemon.SetAction(parsed => {
            state.Command = CommandNames.ServerNodaemon;
            state.Options.ServerListen = parsed.GetValue(listen);
            state.Options.ServerAccessToken = parsed.GetValue(token);
            state.Options.ServerHostName =
                parsed.GetValue(hostName);
            state.Options.ServerIdleSeconds =
                parsed.GetValue(idleTimeout);
            state.Options.ServerMaxAttachmentMiB =
                parsed.GetValue(maxAttachment);
            state.Options.ServerMaxRequestMiB =
                parsed.GetValue(maxRequest);
            state.Options.ServerSerialHistoryKiB =
                parsed.GetValue(serialHistory);
            return 0;
        });
        server.Add(nodaemon);
        root.Add(server);
        root.Add(CreateDiscoverCommand(state));
    }

    private static Command CreateDiscoverCommand(
        CommandParseState state) {
        var timeout = new Option<int>("--timeout") {
            Description = "Discovery wait time in seconds.",
            DefaultValueFactory = _ => 2
        };
        var command = new Command(
            CommandNames.Discover,
            "Find toolkit servers on the local network.");
        command.Add(timeout);
        command.SetAction(parsed => {
            state.Command = CommandNames.Discover;
            state.Options.DiscoverySeconds =
                parsed.GetValue(timeout);
            return 0;
        });
        return command;
    }
}
