namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

internal sealed class HexUInt32Parser :
    ICommandValueParser<uint> {
    public uint Parse(string name, string value) =>
        ValueParser.ParseUInt32(name, value);
}
