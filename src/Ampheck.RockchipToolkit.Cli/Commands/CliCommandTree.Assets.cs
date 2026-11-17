using System.CommandLine;
using System.Globalization;
using Ampheck.RockchipToolkit.Cli.Assets.Model;
using Ampheck.RockchipToolkit.Cli.Assets.Providers;
using Ampheck.RockchipToolkit.Cli.Assets.Services;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private Command BuildAssets() {
        var group = new Command(
            "assets",
            "Acquire and manage external tool assets.") {
            BuildAssetEnsure(),
            BuildAssetForget(),
            BuildAssetList(),
            BuildAssetPrune(),
            BuildAssetRoot(),
            BuildAssetVerify()
        };
        return group;
    }

    private Command BuildAssetRoot() {
        var command = new Command(
            "root",
            "Print the asset cache root.");
        command.SetAction((_, _) => {
            var assets = CreateAssetManager();
            Console.WriteLine(assets.Root);
            return Task.FromResult(0);
        });
        return command;
    }

    private Command BuildAssetEnsure() {
        var command = new Command(
            "ensure",
            "Download, verify, and cache manifest assets.");
        var manifest = RequiredOption<string>(
            "--manifest",
            "TOML asset manifest.");
        var output = new Option<string?>("--output") {
            Description = "Materialize assets into this directory."
        };
        var offline = new Option<bool>("--offline") {
            Description = "Use only cached assets."
        };
        var link = new Option<bool>("--link", "--hardlink") {
            Description = "Use hardlinks when materializing assets."
        };
        command.Add(manifest);
        command.Add(output);
        command.Add(offline);
        command.Add(link);
        command.SetAction(async (parsed, token) => {
            var assets = CreateAssetManager();
            try {
                var result = await assets.EnsureAsync(
                        parsed.GetValue(manifest)!,
                        parsed.GetValue(output),
                        parsed.GetValue(offline),
                        parsed.GetValue(link),
                        token)
                    .ConfigureAwait(false);
                Console.WriteLine($"CACHE {assets.Root}");
                Console.WriteLine(
                    $"LICENSE {result.License.Name} {result.License.Url}");
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
                WriteAssetErrors(
                    error is AssetManifestException manifestError
                        ? manifestError.Errors
                        : ((AssetOperationException)error).Errors);
                return 1;
            }
        });
        return command;
    }

    private Command BuildAssetVerify() {
        var command = new Command(
            "verify",
            "Verify a manifest against the asset cache.");
        var manifest = RequiredOption<string>(
            "--manifest",
            "TOML asset manifest.");
        var offline = new Option<bool>("--offline") {
            Description = "No-op; verify never accesses the network."
        };
        command.Add(manifest);
        command.Add(offline);
        command.SetAction(async (parsed, token) => {
            _ = parsed.GetValue(offline);
            var assets = CreateAssetManager();
            try {
                var report = await assets.VerifyAsync(
                        parsed.GetValue(manifest)!,
                        token)
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
                WriteAssetErrors(error.Errors);
                return 1;
            }
        });
        return command;
    }

    private Command BuildAssetForget() {
        var command = new Command(
            "forget",
            "Forget a manifest's cached reference record.");
        var manifest = RequiredOption<string>(
            "--manifest",
            "TOML asset manifest.");
        command.Add(manifest);
        command.SetAction(async (parsed, token) => {
            var assets = CreateAssetManager();
            var forgotten = await assets.ForgetAsync(
                    parsed.GetValue(manifest)!,
                    token)
                .ConfigureAwait(false);
            Console.WriteLine(
                forgotten
                    ? $"FORGOTTEN {Path.GetFullPath(parsed.GetValue(manifest)!)}"
                    : $"NOT-REFERENCED {Path.GetFullPath(parsed.GetValue(manifest)!)}");
            return 0;
        });
        return command;
    }

    private Command BuildAssetList() {
        var command = new Command(
            "list",
            "List cached assets and manifest references.");
        command.SetAction(async (_, token) => {
            var assets = CreateAssetManager();
            var (cached, references) = await assets.ListAsync(token)
                .ConfigureAwait(false);
            var referencesByHash = references
                .SelectMany(reference => reference.Assets.Select(asset => (
                    Reference: reference,
                    Asset: asset)))
                .GroupBy(
                    pair => pair.Asset.Sha256,
                    StringComparer.Ordinal)
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
                    .OrderBy(
                        value => value.Reference.ManifestPath,
                        StringComparer.Ordinal)
                    .ThenBy(
                        value => value.Asset.Output,
                        StringComparer.Ordinal)) {
                    Console.WriteLine(
                        $"  referenced-by " +
                        $"{reference.Reference.ManifestPath} " +
                        $"{reference.Asset.Output}: " +
                        reference.Asset.Purpose);
                }
            }

            Console.WriteLine($"ASSETS {cached.Count}");
            return 0;
        });
        return command;
    }

    private Command BuildAssetPrune() {
        var command = new Command(
            "prune",
            "Show or delete unreferenced cached assets.");
        var dryRun = new Option<bool>("--dry-run") {
            Description = "Only print the deletion plan."
        };
        var yes = new Option<bool>("--yes") {
            Description = "Delete planned assets."
        };
        command.Add(dryRun);
        command.Add(yes);
        command.SetAction(async (parsed, token) => {
            var assets = CreateAssetManager();
            var plan = await assets.PruneAsync(
                    parsed.GetValue(dryRun),
                    parsed.GetValue(yes),
                    token)
                .ConfigureAwait(false);
            foreach (var entry in plan.Entries) {
                Console.WriteLine(
                    $"PRUNE {entry.Sha256} {entry.Length} {entry.Path}");
            }

            Console.WriteLine(
                $"PLAN {plan.Entries.Count} asset(s), " +
                $"{plan.TotalBytes.ToString(
                    CultureInfo.InvariantCulture)} bytes");
            if (parsed.GetValue(dryRun) || !parsed.GetValue(yes)) {
                Console.WriteLine(
                    "NOTICE no files deleted; use --yes to delete");
                return 0;
            }

            foreach (var entry in plan.Entries) {
                Console.WriteLine($"DELETED {entry.Sha256}");
            }

            return 0;
        });
        return command;
    }

    private static AssetManager CreateAssetManager() {
        var paths = AssetCachePaths.Create();
        var provider = new RockchipRkbinProvider();
        var providers = new AssetProviderRegistry([provider]);
        return new AssetManager(
            new AssetManifestParser(providers),
            providers,
            new AssetCache(paths),
            new AssetReferenceStore(paths));
    }

    private static void WriteAssetErrors(IEnumerable<string> errors) {
        foreach (var error in errors) {
            Console.Error.WriteLine($"error: {error}");
        }
    }
}
