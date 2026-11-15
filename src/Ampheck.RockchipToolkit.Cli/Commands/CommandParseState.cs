namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class CommandParseState {
    public CliInvocation? Invocation { get; set; }
    public bool HelpDisplayed { get; set; }
    public string[] Arguments { get; set; } = [];
}
