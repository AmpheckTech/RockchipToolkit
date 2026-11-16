using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Completion;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Completion;

internal sealed class CompletionCommand {
    [CommandHandler(
        ["completion"],
        Description = "Print a shell completion script.")]
    private ValueTask<int> ExecuteAsync(
        [CommandParam(
            ["shell"],
            Description = "bash, fish, powershell, or zsh.",
            Choices = ["bash", "fish", "powershell", "zsh"])]
        string shell
    ) {
        Console.Out.Write(CompletionScripts.Get(shell));
        Console.Out.Flush();
        return ValueTask.FromResult(0);
    }
}
