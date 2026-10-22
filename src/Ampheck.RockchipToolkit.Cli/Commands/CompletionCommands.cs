using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class CompletionCommands {
    private static readonly string[] Shells = [
        "bash",
        "zsh",
        "powershell",
        "fish"
    ];

    public static Command Create(CommandParseState state) {
        var command = new Command(
            CommandNames.Completion,
            "Print a shell completion script.");
        var shell = new Argument<string>("shell") {
            Description = string.Join(", ", Shells)
        };
        shell.AcceptOnlyFromAmong(Shells);
        command.Add(shell);
        command.SetAction(parsed => {
            state.Command = CommandNames.Completion;
            state.Options.CompletionShell =
                parsed.GetValue(shell);
            return 0;
        });
        return command;
    }
}
