using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class RockUsbOptionSet(
    bool allowLoader,
    bool allowFetch) {
    private static readonly string[] TransportKinds = [
        "auto",
        "rockusb",
        "libusb"
    ];
    private static readonly string[] FetchLocations = [
        "server",
        "client"
    ];
    private readonly Option<string?> _transport = new(
        "--transport",
        "-t") {
        Description = string.Join(", ", TransportKinds)
    };
    private readonly Option<string?> _vendorId = new("--vid") {
        Description = "USB vendor ID."
    };
    private readonly Option<string?> _productId = new("--pid") {
        Description = "USB product ID."
    };
    private readonly Option<bool> _verbose = new("--verbose", "-v") {
        Description = "Enable informational and debug logs."
    };
    private readonly Option<string?> _loader = new(
        "--loader",
        "-l") {
        Description = "Download this loader before the command."
    };
    private readonly Option<string?> _fetch = new("--fetch") {
        Description =
            "Fetch HTTP(S) files on the server (default) or client."
    };

    public void AddTo(Command command) {
        _transport.AcceptOnlyFromAmong(TransportKinds);
        _fetch.AcceptOnlyFromAmong(FetchLocations);
        command.Add(_transport);
        command.Add(_vendorId);
        command.Add(_productId);
        command.Add(_verbose);
        if (allowLoader) {
            command.Add(_loader);
        }

        if (allowFetch) {
            command.Add(_fetch);
        }
    }

    public RockUsbOptionValues Parse(ParseResult parsed) {
        return new RockUsbOptionValues(
            new DeviceTargetArguments(
                ParseTransport(
                    parsed.GetValue(_transport) ?? "auto"),
                ValueParser.ParseOptionalUInt16(
                    "--vid",
                    parsed.GetValue(_vendorId)),
                ValueParser.ParseOptionalUInt16(
                    "--pid",
                    parsed.GetValue(_productId))),
            allowLoader
                ? parsed.GetValue(_loader)
                : null,
            allowFetch
                ? ParseFetchLocation(
                    parsed.GetValue(_fetch))
                : InputFetchLocation.Server);
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

internal sealed record RockUsbOptionValues(
    DeviceTargetArguments Target,
    string? Loader,
    InputFetchLocation FetchLocation);
