using Ampheck.RockchipToolkit.Arkt.Commands.Framework;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb;

internal class RockUsbLoaderParameters : RockUsbParameters {
    [CommandParam(
        ["--loader", "-l"],
        Description = "Download this loader before the command.")]
    public string? Loader { get; init; }
}
