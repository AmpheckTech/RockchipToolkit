namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class CommandGroupAttribute(string[] path) : Attribute {
    public IReadOnlyList<string> Path { get; } = [.. path];
    public string Description { get; init; } = "";
}
