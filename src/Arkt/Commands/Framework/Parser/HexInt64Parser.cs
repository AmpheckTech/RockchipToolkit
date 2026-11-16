namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

internal sealed class HexInt64Parser :
    ICommandValueParser<long?> {
    public long? Parse(string name, string value) =>
        ValueParser.ParseOptionalInt64(name, value);
}
