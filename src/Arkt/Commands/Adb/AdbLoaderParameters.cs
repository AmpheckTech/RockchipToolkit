using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Adb;

internal sealed class AdbLoaderParameters : AdbCommandParameters {
    [CommandParam(
        ["--vid"],
        Description = "RockUSB Loader vendor ID.",
        Parser = typeof(HexUInt16Parser))]
    public ushort? VendorId { get; init; }

    [CommandParam(
        ["--pid"],
        Description = "RockUSB Loader product ID.",
        Parser = typeof(HexUInt16Parser))]
    public ushort? ProductId { get; init; }
}
