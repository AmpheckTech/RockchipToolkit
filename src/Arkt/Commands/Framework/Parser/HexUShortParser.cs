namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

internal sealed class HexUShortParser :
    ICommandValueParser<ushort> {
    public ushort Parse(string name, string value) =>
        ValueParser.ParseUInt16(name, value);
}
