namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

internal interface ICommandValueParser<out T> {
    T Parse(string name, string value);
}
