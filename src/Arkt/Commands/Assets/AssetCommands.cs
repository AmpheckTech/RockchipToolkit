using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support.Assets;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Assets;

[CommandGroup(
    ["assets"],
    Description = "Acquire and manage external tool assets.")]
internal sealed class AssetCommands(AssetCommandRunner runner) {
    [CommandHandler(
        ["assets", "root"],
        Description = "Print the asset cache root.")]
    private ValueTask<int> ExecuteRootAsync(
        CancellationToken cancellationToken
    ) => new(runner.Root());

    [CommandHandler(
        ["assets", "ensure"],
        Description = "Download, verify, and cache manifest assets.")]
    private ValueTask<int> ExecuteEnsureAsync(
        [CommandParam(
            ["--manifest"],
            Required = true,
            Description = "TOML asset manifest.")]
        string manifest,
        [CommandParam(
            ["--output"],
            Description = "Materialize assets into this directory.")]
        string? output,
        [CommandParam(
            ["--offline"],
            Description = "Use only cached assets.")]
        bool offline,
        [CommandParam(
            ["--link", "--hardlink"],
            Description = "Use hardlinks when materializing assets.")]
        bool useHardLink,
        CancellationToken cancellationToken
    ) => new(runner.EnsureAsync(
        manifest,
        output,
        offline,
        useHardLink,
        cancellationToken));

    [CommandHandler(
        ["assets", "verify"],
        Description = "Verify a manifest against the asset cache.")]
    private ValueTask<int> ExecuteVerifyAsync(
        [CommandParam(
            ["--manifest"],
            Required = true,
            Description = "TOML asset manifest.")]
        string manifest,
        [CommandParam(
            ["--offline"],
            Description = "No-op; verify never accesses the network.")]
        bool offline,
        CancellationToken cancellationToken
    ) => new(runner.VerifyAsync(
        manifest,
        offline,
        cancellationToken));

    [CommandHandler(
        ["assets", "forget"],
        Description = "Forget a manifest's cached reference record.")]
    private ValueTask<int> ExecuteForgetAsync(
        [CommandParam(
            ["--manifest"],
            Required = true,
            Description = "TOML asset manifest.")]
        string manifest,
        CancellationToken cancellationToken
    ) => new(runner.ForgetAsync(
        manifest,
        cancellationToken));

    [CommandHandler(
        ["assets", "list"],
        Description = "List cached assets and manifest references.")]
    private ValueTask<int> ExecuteListAsync(
        CancellationToken cancellationToken
    ) => new(runner.ListAsync(cancellationToken));

    [CommandHandler(
        ["assets", "prune"],
        Description = "Show or delete unreferenced cached assets.")]
    private ValueTask<int> ExecutePruneAsync(
        [CommandParam(
            ["--dry-run"],
            Description = "Only print the deletion plan.")]
        bool dryRun,
        [CommandParam(
            ["--yes"],
            Description = "Delete planned assets.")]
        bool yes,
        CancellationToken cancellationToken
    ) => new(runner.PruneAsync(
        dryRun,
        yes,
        cancellationToken));
}
