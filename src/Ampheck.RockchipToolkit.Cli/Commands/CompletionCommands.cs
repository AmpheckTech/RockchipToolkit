using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Infrastructure;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class CompletionCommands(CommandParseState state) {
    private static readonly string[] Shells = [
        "bash",
        "zsh",
        "powershell",
        "fish"
    ];

    public void Register(RootCommand root) {
        var command = new Command(
            CommandNames.Completion,
            "Print a shell completion script.");
        var shell = new Argument<string>("shell") {
            Description = string.Join(", ", Shells)
        };
        shell.AcceptOnlyFromAmong(Shells);
        command.Add(shell);
        command.SetAction(parsed => {
            var selectedShell = parsed.GetValue(shell);
            state.Invocation = new CliInvocation(
                CommandNames.Completion,
                RequiresServer: false,
                (context, _) => {
                    Console.Out.Write(CompletionScripts.Get(
                        selectedShell!));
                    Console.Out.Flush();
                    return Task.FromResult(0);
                });
            return 0;
        });
        root.Add(command);
    }
}
