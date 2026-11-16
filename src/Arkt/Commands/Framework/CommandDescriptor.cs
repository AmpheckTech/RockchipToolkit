using System.Reflection;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed record CommandDescriptor(
    Type Type,
    IReadOnlyList<string> Path,
    string Description,
    IReadOnlyList<string> Aliases,
    bool TreatUnmatchedTokensAsErrors,
    MethodInfo? Handler
) {
    public bool HasAction => Handler is not null;

    public string Name => Path[^1];

    public string CommandName =>
        string.Join(' ', Path);
}
