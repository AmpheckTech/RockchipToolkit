using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Images.Update;

public static class RockchipImageVerifier {
    public static async ValueTask<RockchipUpdateImageKind> VerifyAsync(
        string path,
        IProgress<RockchipImageProgress>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var update = RockchipUpdateImage.Open(path);
        var reporter = new RockchipImageProgressReporter(progress);
        await RockchipImageChecksum.ValidateAsync(
                path,
                update,
                reporter,
                cancellationToken)
            .ConfigureAwait(false);
        reporter.Complete();
        return update.Kind;
    }
}
