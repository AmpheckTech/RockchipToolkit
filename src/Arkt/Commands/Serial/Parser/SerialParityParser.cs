using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial.Parser;

internal sealed class SerialParityParser :
    ICommandValueParser<SerialParity> {
    public SerialParity Parse(string name, string value) =>
        value.ToLowerInvariant() switch {
            "odd" => SerialParity.Odd,
            "even" => SerialParity.Even,
            "mark" => SerialParity.Mark,
            "space" => SerialParity.Space,
            _ => SerialParity.None
        };
}
