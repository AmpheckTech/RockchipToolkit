using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial.Parser;

internal sealed class SerialStopBitsParser :
    ICommandValueParser<SerialStopBits> {
    public SerialStopBits Parse(string name, string value) =>
        value.ToLowerInvariant() switch {
            "one-point-five" => SerialStopBits.OnePointFive,
            "two" => SerialStopBits.Two,
            _ => SerialStopBits.One
        };
}
