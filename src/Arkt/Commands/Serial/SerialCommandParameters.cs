using Ampheck.RockchipToolkit.Arkt.Commands.Framework;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial;

internal class SerialCommandParameters {
    [CommandParam(
        ["--verbose", "-v"],
        Description = "Enable informational and debug logging.")]
    public bool Verbose { get; init; }
}
