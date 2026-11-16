namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed record HandlerBinding(
    int Index,
    Injection Injection,
    Type? GroupType,
    IReadOnlyList<CommandParameter> Members
);
