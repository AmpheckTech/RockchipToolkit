namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record ParsedCommand(
    string Command,
    CommandOptions Options);
