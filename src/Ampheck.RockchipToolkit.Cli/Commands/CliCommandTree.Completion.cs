using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Completion;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private static Command BuildCompletion() {
        var shell = CreateArgument<string>(
            "shell",
            "bash, fish, powershell, or zsh.");
        var command = new Command(
            "completion",
            "Print a shell completion script.") {
            shell
        };
        command.SetAction((parsed, _) => {
            Console.Out.Write(
                CompletionScripts.Get(parsed.GetValue(shell)!));
            Console.Out.Flush();
            return Task.FromResult(0);
        });
        return command;
    }
}
