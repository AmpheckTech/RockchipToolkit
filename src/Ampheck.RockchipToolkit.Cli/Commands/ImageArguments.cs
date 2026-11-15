namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record ImageCommandArguments(
    string Input,
    string Output,
    bool Force,
    string? Chip,
    bool Unpack);
