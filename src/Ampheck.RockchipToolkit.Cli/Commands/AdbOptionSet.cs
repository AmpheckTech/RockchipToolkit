using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class AdbOptionSet(
    bool includeLoaderFilters = false) {
    private readonly Option<string?> _serial = new(
        "--serial",
        "-s") {
        Description = "ADB device serial number."
    };
    private readonly Option<bool> _verbose = new("--verbose", "-v") {
        Description = "Enable informational and debug logs."
    };
    private readonly Option<string?> _vendorId = new("--vid") {
        Description = "RockUSB Loader vendor ID."
    };
    private readonly Option<string?> _productId = new("--pid") {
        Description = "RockUSB Loader product ID."
    };

    public void AddTo(Command command) {
        command.Add(_serial);
        command.Add(_verbose);
        if (includeLoaderFilters) {
            command.Add(_vendorId);
            command.Add(_productId);
        }
    }

    public AdbOptionValues Parse(ParseResult parsed) {
        return new AdbOptionValues(
            parsed.GetValue(_serial),
            new DeviceTargetArguments(
                TransportKind.Auto,
                includeLoaderFilters
                    ? ValueParser.ParseOptionalUInt16(
                        "--vid",
                        parsed.GetValue(_vendorId))
                    : null,
                includeLoaderFilters
                    ? ValueParser.ParseOptionalUInt16(
                        "--pid",
                        parsed.GetValue(_productId))
                    : null));
    }
}

internal sealed record AdbOptionValues(
    string? SerialNumber,
    DeviceTargetArguments LoaderTarget);
