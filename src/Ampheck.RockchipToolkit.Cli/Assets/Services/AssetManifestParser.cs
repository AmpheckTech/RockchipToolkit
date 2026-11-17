using Ampheck.RockchipToolkit.Cli.Assets.Model;
using Ampheck.RockchipToolkit.Cli.Assets.Providers;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal sealed class AssetManifestParser(
    AssetProviderRegistry providers
) {
    public async ValueTask<AssetManifest> ParseAsync(
        string manifestPath,
        CancellationToken cancellationToken
    ) {
        var path = Path.GetFullPath(manifestPath);
        string text;
        try {
            text = await File.ReadAllTextAsync(path, cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
            throw new AssetManifestException([
                $"Unable to read manifest '{path}': {error.Message}"
            ]);
        }

        AssetManifestDocument document;
        try {
            document = Tomlyn.TomlSerializer.Deserialize(
                    text,
                    AssetTomlContext.Default.AssetManifestDocument) ??
                throw new InvalidDataException("The manifest is empty.");
        } catch (Exception error) when (
            error is Tomlyn.TomlException or InvalidDataException) {
            throw new AssetManifestException([
                $"Invalid TOML in '{path}': {error.Message}"
            ]);
        }

        var errors = new List<string>();
        ValidateUnknownKeys(
            document.Unknown,
            "manifest",
            errors);
        var schema = document.Schema == 0 ? (long?)null : document.Schema;
        if (schema is null) {
            errors.Add("manifest: field 'schema' must be an integer.");
        }

        var providerId = string.IsNullOrWhiteSpace(document.Provider)
            ? null
            : document.Provider;
        if (providerId is null) {
            errors.Add("manifest: field 'provider' must be a string.");
        }

        IAssetProvider? provider = null;
        if (!string.IsNullOrWhiteSpace(providerId)) {
            try {
                provider = providers.Get(providerId);
            } catch (AssetManifestException error) {
                errors.AddRange(error.Errors);
            }
        }

        var assets = new List<AssetEntry>();
        if (document.Assets.Count == 0) {
            errors.Add("manifest: at least one asset is required.");
        }

        for (var index = 0; index < document.Assets.Count; index++) {
            var prefix = $"assets[{index}]";
            var asset = document.Assets[index];
            ValidateUnknownKeys(asset.Unknown, prefix, errors);
            if (asset.Commit is not null &&
                asset.Path is not null &&
                asset.Output is not null &&
                asset.Sha256 is not null &&
                asset.Purpose is not null) {
                ValidateCommon(
                    asset.Commit,
                    asset.Path,
                    asset.Output,
                    asset.Sha256,
                    asset.Purpose,
                    prefix,
                    errors);
                var entry = new AssetEntry(
                    asset.Commit,
                    asset.Path,
                    asset.Output,
                    asset.Sha256,
                    asset.Purpose,
                    asset.Optional);
                if (provider is not null) {
                    try {
                        provider.Validate(entry);
                    } catch (AssetManifestException error) {
                        errors.AddRange(error.Errors);
                    }
                }

                assets.Add(entry);
            }
        }

        if (schema is not null && schema != 1) {
            errors.Add("manifest: schema must be 1.");
        }

        var duplicateOutput = assets
            .GroupBy(asset => asset.Output, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOutput is not null) {
            errors.Add(
                $"manifest: output '{duplicateOutput.Key}' is declared " +
                "more than once.");
        }

        if (errors.Count > 0) {
            throw new AssetManifestException(errors);
        }

        return new AssetManifest(
            checked((int)schema!.Value),
            providerId!,
            assets);
    }

    private static void ValidateCommon(
        string commit,
        string upstreamPath,
        string output,
        string sha256,
        string purpose,
        string prefix,
        ICollection<string> errors
    ) {
        _ = commit;
        if (AssetPathValidator.ValidateRelativePath(
                upstreamPath,
                $"{prefix}.path") is { } pathError) {
            errors.Add(pathError);
        }

        if (AssetPathValidator.ValidateRelativePath(
                output,
                $"{prefix}.output") is { } outputError) {
            errors.Add(outputError);
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                sha256,
                "^[0-9a-f]{64}$")) {
            errors.Add(
                $"{prefix}.sha256 must be 64 lowercase hexadecimal " +
                "characters.");
        }

        if (string.IsNullOrWhiteSpace(purpose)) {
            errors.Add($"{prefix}.purpose must not be empty.");
        }
    }

    private static void ValidateUnknownKeys(
        IReadOnlyDictionary<string, object>? unknown,
        string prefix,
        ICollection<string> errors
    ) {
        if (unknown is null) {
            return;
        }

        foreach (var key in unknown.Keys) {
            errors.Add($"{prefix}: unknown field '{key}'.");
        }
    }
}
