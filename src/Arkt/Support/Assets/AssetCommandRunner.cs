using System.Globalization;
using Ampheck.RockchipToolkit.Features.Assets.Model;
using Ampheck.RockchipToolkit.Features.Assets.Services;

namespace Ampheck.RockchipToolkit.Arkt.Support.Assets;

internal sealed class AssetCommandRunner(
    AssetManager assets
) {
    public int Root() {
        Console.WriteLine(assets.Root);
        return 0;
    }

    public async Task<int> EnsureAsync(
        string manifest,
        string? output,
        bool offline,
        bool useHardLink,
        CancellationToken cancellationToken
    ) {
        try {
            var result = await assets.EnsureAsync(
                    manifest,
                    output,
                    offline,
                    useHardLink,
                    cancellationToken)
                .ConfigureAwait(false);
            Console.WriteLine($"CACHE {assets.Root}");
            Console.WriteLine($"LICENSE {result.License.Name} {result.License.Url}");
            foreach (var entry in result.Assets) {
                Console.WriteLine(
                    $"{entry.Sha256} {entry.Length} {entry.Path}");
            }

            foreach (var warning in result.Warnings) {
                Console.Error.WriteLine($"warning: {warning}");
            }

            return 0;
        } catch (Exception error) when (
            error is AssetManifestException or AssetOperationException) {
            WriteErrors(
                error is AssetManifestException manifestError
                    ? manifestError.Errors
                    : ((AssetOperationException)error).Errors);
            return 1;
        }
    }

    public async Task<int> VerifyAsync(
        string manifest,
        bool offline,
        CancellationToken cancellationToken
    ) {
        _ = offline;
        try {
            var report = await assets.VerifyAsync(
                    manifest,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var warning in report.Warnings) {
                Console.Error.WriteLine($"warning: {warning}");
            }

            foreach (var error in report.Errors) {
                Console.Error.WriteLine($"error: {error}");
            }

            Console.WriteLine(
                $"VERIFY {report.Errors.Count} error(s), " +
                $"{report.Warnings.Count} warning(s)");
            return report.Errors.Count == 0 ? 0 : 1;
        } catch (AssetManifestException error) {
            WriteErrors(error.Errors);
            return 1;
        }
    }

    public async Task<int> ForgetAsync(
        string manifest,
        CancellationToken cancellationToken
    ) {
        var forgotten = await assets.ForgetAsync(
                manifest,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            forgotten
                ? $"FORGOTTEN {Path.GetFullPath(manifest)}"
                : $"NOT-REFERENCED {Path.GetFullPath(manifest)}");
        return 0;
    }

    public async Task<int> ListAsync(
        CancellationToken cancellationToken
    ) {
        var (cached, references) = await assets.ListAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var referencesByHash = references
            .SelectMany(reference => reference.Assets.Select(asset => (
                Reference: reference,
                Asset: asset)))
            .GroupBy(pair => pair.Asset.Sha256, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        Console.WriteLine($"CACHE {assets.Root}");
        foreach (var entry in cached) {
            Console.WriteLine(
                $"{entry.Sha256} {entry.Length} {entry.Path}");
            if (!referencesByHash.TryGetValue(
                    entry.Sha256,
                    out var usedBy)) {
                Console.WriteLine("  referenced-by -");
                continue;
            }

            foreach (var reference in usedBy
                .OrderBy(value => value.Reference.ManifestPath,
                    StringComparer.Ordinal)
                .ThenBy(value => value.Asset.Output,
                    StringComparer.Ordinal)) {
                Console.WriteLine(
                    $"  referenced-by {reference.Reference.ManifestPath} " +
                    $"{reference.Asset.Output}: {reference.Asset.Purpose}");
            }
        }

        Console.WriteLine($"ASSETS {cached.Count}");
        return 0;
    }

    public async Task<int> PruneAsync(
        bool dryRun,
        bool yes,
        CancellationToken cancellationToken
    ) {
        var plan = await assets.PruneAsync(
                dryRun,
                yes,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in plan.Entries) {
            Console.WriteLine(
                $"PRUNE {entry.Sha256} {entry.Length} {entry.Path}");
        }

        Console.WriteLine(
            $"PLAN {plan.Entries.Count} asset(s), " +
            $"{plan.TotalBytes.ToString(CultureInfo.InvariantCulture)} bytes");
        if (dryRun || !yes) {
            Console.WriteLine("NOTICE no files deleted; use --yes to delete");
            return 0;
        }

        foreach (var entry in plan.Entries) {
            Console.WriteLine($"DELETED {entry.Sha256}");
        }

        return 0;
    }

    private static void WriteErrors(IEnumerable<string> errors) {
        foreach (var error in errors) {
            Console.Error.WriteLine($"error: {error}");
        }
    }
}
