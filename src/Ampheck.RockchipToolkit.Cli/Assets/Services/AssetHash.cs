using System.Security.Cryptography;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal static class AssetHash {
    public static async ValueTask<string> ComputeAsync(
        string path,
        CancellationToken cancellationToken
    ) {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
