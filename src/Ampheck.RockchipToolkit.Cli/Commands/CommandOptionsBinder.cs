using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class CommandOptionsBinder {
    private static readonly string[] TransportKinds = [
        "auto",
        "rockusb",
        "libusb"
    ];
    private static readonly string[] FetchLocations = [
        "server",
        "client"
    ];

    public static void AddRockUsbOptions(
        Command command,
        CommandParseState state,
        string canonicalCommand,
        bool allowLoader = false,
        bool allowFetch = false,
        Action<ParseResult>? bind = null) {
        var transport = new Option<string?>(
            "--transport",
            "-t") {
            Description = string.Join(", ", TransportKinds)
        };
        transport.AcceptOnlyFromAmong(TransportKinds);
        var vid = new Option<string?>("--vid") {
            Description = "USB vendor ID."
        };
        var pid = new Option<string?>("--pid") {
            Description = "USB product ID."
        };
        var verbose = new Option<bool>("--verbose", "-v") {
            Description = "Enable informational and debug logs."
        };
        command.Add(transport);
        command.Add(vid);
        command.Add(pid);
        command.Add(verbose);

        Option<string?>? fetch = null;
        if (allowFetch) {
            fetch = new Option<string?>("--fetch") {
                Description =
                    "Fetch HTTP(S) files on the server (default) or client."
            };
            fetch.AcceptOnlyFromAmong(FetchLocations);
            command.Add(fetch);
        }

        Option<string?>? loader = null;
        if (allowLoader) {
            loader = new Option<string?>(
                "--loader",
                "-l") {
                Description = "Download this loader before the command."
            };
            command.Add(loader);
        }

        command.SetAction(parsed => {
            state.Command = canonicalCommand;
            state.Options.Transport = ParseTransport(
                parsed.GetValue(transport) ?? "auto");
            state.Options.VendorId = ValueParser.ParseOptionalUInt16(
                "--vid",
                parsed.GetValue(vid));
            state.Options.ProductId = ValueParser.ParseOptionalUInt16(
                "--pid",
                parsed.GetValue(pid));
            state.Options.FetchLocation = ParseFetchLocation(
                fetch is null
                    ? null
                    : parsed.GetValue(fetch));
            state.Options.Loader = loader is null
                ? null
                : parsed.GetValue(loader);
            bind?.Invoke(parsed);
            return 0;
        });
    }

    public static void AddAdbOptions(
        Command command,
        CommandParseState state,
        string canonicalCommand,
        Action<ParseResult>? bind = null) {
        var vid = new Option<string?>("--vid") {
            Description = "USB vendor ID."
        };
        var pid = new Option<string?>("--pid") {
            Description = "USB product ID."
        };
        var serial = new Option<string?>(
            "--serial",
            "-s") {
            Description = "ADB device serial number."
        };
        var verbose = new Option<bool>("--verbose", "-v") {
            Description = "Enable informational and debug logs."
        };
        command.Add(vid);
        command.Add(pid);
        command.Add(serial);
        command.Add(verbose);
        command.SetAction(parsed => {
            state.Command = canonicalCommand;
            state.Options.VendorId = ValueParser.ParseOptionalUInt16(
                "--vid",
                parsed.GetValue(vid));
            state.Options.ProductId = ValueParser.ParseOptionalUInt16(
                "--pid",
                parsed.GetValue(pid));
            state.Options.AdbSerial = parsed.GetValue(serial);
            bind?.Invoke(parsed);
            return 0;
        });
    }

    public static void AddSerialEndpointOptions(
        Command command,
        CommandParseState state,
        string canonicalCommand,
        Action<ParseResult>? bind = null) {
        var verbose = new Option<bool>("--verbose", "-v") {
            Description = "Enable informational and debug logs."
        };
        command.Add(verbose);
        command.SetAction(parsed => {
            state.Command = canonicalCommand;
            bind?.Invoke(parsed);
            return 0;
        });
    }

    private static TransportKind ParseTransport(string value) {
        return value.ToLowerInvariant() switch {
            "auto" => TransportKind.Auto,
            "rockusb" => TransportKind.RockUsb,
            "libusb" => TransportKind.LibUsb,
            _ => throw new ArgumentException(
                $"Invalid transport '{value}'.")
        };
    }

    private static InputFetchLocation ParseFetchLocation(
        string? value) {
        return value?.ToLowerInvariant() switch {
            "client" => InputFetchLocation.Client,
            _ => InputFetchLocation.Server
        };
    }

}
