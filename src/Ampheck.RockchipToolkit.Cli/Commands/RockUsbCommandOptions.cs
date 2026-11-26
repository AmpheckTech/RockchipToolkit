using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Commands.Parsers;
using Ampheck.RockchipToolkit.Devices;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class RockUsbCommandOptions {
    public Option<string> Transport { get; }
    public Option<ushort?> VendorId { get; }
    public Option<ushort?> ProductId { get; }

    public RockUsbCommandOptions(bool includeLoader = false) {
#if ROCKUSB
        Transport = new("--transport", "-t") {
            Description = "auto, rockusb, or libusb.",
            DefaultValueFactory = _ => "auto"
        };
#else
        Transport = new("--transport", "-t") {
            Description = "auto or libusb.",
            DefaultValueFactory = _ => "auto"
        };
#endif
        VendorId = new("--vid") {
            Description = "USB vendor ID."
        };
        VendorId.CustomParser = result => ValueParser.ParseUInt16(
            "--vid",
            result.Tokens.Single().Value);
        ProductId = new("--pid") {
            Description = "USB product ID."
        };
        ProductId.CustomParser = result => ValueParser.ParseUInt16(
            "--pid",
            result.Tokens.Single().Value);
        if (includeLoader) {
            Loader = new("--loader", "-l") {
                Description = "Download this RAM loader before the command."
            };
        }
    }

    public Option<string?>? Loader { get; }

    public void AddTo(Command command) {
        command.Add(Transport);
        command.Add(VendorId);
        command.Add(ProductId);
        if (Loader is not null) {
            command.Add(Loader);
        }
    }

    public DeviceTarget ReadTarget(ParseResult parsed) => new() {
        Transport = ValueParser.ParseTransport(
            "--transport",
            parsed.GetValue(Transport)!),
        VendorId = parsed.GetValue(VendorId),
        ProductId = parsed.GetValue(ProductId)
    };
}
