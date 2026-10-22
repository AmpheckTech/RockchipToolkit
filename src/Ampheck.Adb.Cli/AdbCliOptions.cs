using System.CommandLine;
using Ampheck.Adb.Server;

namespace Ampheck.Adb.Cli;

internal sealed class AdbCliOptions {
    private static readonly AdbServerOptions DefaultServer =
        AdbEndpointEnvironment.Resolve();
    private readonly Option<string?> _serial = new("--serial", "-s") {
        Description = "Target device serial number."
    };
    private readonly Option<string> _host = new("--host", "-H") {
        Description = "ADB server host.",
        DefaultValueFactory = _ => DefaultServer.Host
    };
    private readonly Option<int> _port = new("--port", "-P") {
        Description = "ADB server TCP port.",
        DefaultValueFactory = _ => DefaultServer.Port
    };
    private readonly Option<bool> _direct = new("--direct") {
        Description = "Use the USB ADB backend without a server."
    };
    private readonly Option<bool> _verbose = new("--verbose", "-v") {
        Description = "Print diagnostic information."
    };

    public void AddTo(Command command) {
        command.Add(_serial);
        command.Add(_host);
        command.Add(_port);
        command.Add(_direct);
        command.Add(_verbose);
    }

    public ClientSettings Read(ParseResult parseResult) {
        var port = parseResult.GetValue(_port);
        if (port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                "The ADB server port must be between 1 and 65535.");
        }

        return new ClientSettings(
            parseResult.GetValue(_serial),
            parseResult.GetValue(_host) ?? "127.0.0.1",
            port,
            parseResult.GetValue(_direct),
            parseResult.GetValue(_verbose));
    }

    public AdbServerOptions CreateServerOptions(
        ParseResult parseResult) {
        var settings = Read(parseResult);
        return new AdbServerOptions {
            Host = settings.Host,
            Port = settings.Port
        };
    }
}
