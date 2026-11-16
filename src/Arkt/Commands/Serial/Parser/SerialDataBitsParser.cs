using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial.Parser;

internal sealed class SerialDataBitsParser : ICommandValueParser<int> {
    public int Parse(string name, string value) {
        if (!int.TryParse(value, out var dataBits) ||
            dataBits is < 5 or > 8) {
            throw new ArgumentException(
                $"{name} must be 5, 6, 7, or 8.");
        }

        return dataBits;
    }
}
