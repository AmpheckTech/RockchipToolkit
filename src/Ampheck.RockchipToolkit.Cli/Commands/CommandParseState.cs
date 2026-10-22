namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class CommandParseState {
    public string? Command { get; set; }
    public CommandOptions Options { get; } = new();
}
