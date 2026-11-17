using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Terminal;
using Ampheck.RockchipToolkit.Images.Update;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private Command BuildVerify() {
        var input = CreateArgument<string>(
            "input",
            "RKFW or RKAF image to verify.");
        var command = new Command(
            "verify",
            "Verify image structure and embedded checksums.") {
            input
        };
        command.SetAction((parsed, token) => RunVerifyAsync(
            parsed.GetValue(input)!,
            token));
        return command;
    }

    private async Task<int> RunVerifyAsync(
        string input,
        CancellationToken cancellationToken
    ) {
        using var progress = new ImageProgressDisplay(
            _console,
            "verify");
        _console.StartActivity($"Verifying {input}");
        try {
            var kind = await RockchipImageVerifier.VerifyAsync(
                    input,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            progress.Complete();
            _console.CompleteActivity($"Verified {kind} {input}");
            return 0;
        } finally {
            _console.CompleteActivity();
        }
    }
}
