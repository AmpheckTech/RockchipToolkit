namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

[AttributeUsage(
    AttributeTargets.Parameter |
    AttributeTargets.Property |
    AttributeTargets.Field,
    Inherited = true)]
internal sealed class CommandParamAttribute : Attribute {
    public CommandParamAttribute() {
    }

    public CommandParamAttribute(string name) {
        Names = [name];
    }

    public CommandParamAttribute(string[] names) {
        Names = [.. names];
    }

    public IReadOnlyList<string> Names { get; } = [];
    public string? Description { get; init; }
    public bool Required { get; init; }
    public int Position { get; init; } = -1;
    public Type? Parser { get; init; }
    public string[]? Choices { get; init; }
    public int MinimumArity { get; init; } = 1;
    public int MaximumArity { get; init; } = 1;
}
