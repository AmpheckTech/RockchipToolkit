namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class CommandHandlerAttribute(string[] path) : Attribute {
    public IReadOnlyList<string> Path { get; } = [.. path];
    public string Description { get; init; } = "";
    public string[]? Aliases { get; init; }
    public bool TreatUnmatchedTokensAsErrors { get; init; } = true;
}
