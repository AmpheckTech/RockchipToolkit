using Ampheck.RockchipToolkit.Features.Assets.Model;
using Ampheck.RockchipToolkit.Features.Assets.Providers;
using Tomlyn;
using Tomlyn.Model;

namespace Ampheck.RockchipToolkit.Features.Assets.Services;

internal sealed class AssetManifestParser(
    AssetProviderRegistry providers
) {
    private static readonly string[] RootKeys = [
        "schema",
        "provider",
        "assets"
    ];

    private static readonly string[] AssetKeys = [
        "commit",
        "path",
        "output",
        "sha256",
        "purpose",
        "optional"
    ];

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

        TomlTable model;
        try {
            model = TomlSerializer.Deserialize<TomlTable>(text) ??
                throw new TomlException("The manifest is empty.");
        } catch (TomlException error) {
            throw new AssetManifestException([
                $"Invalid TOML in '{path}': {error.Message}"
            ]);
        }

        var errors = new List<string>();
        ValidateUnknownKeys(model, RootKeys, "manifest", errors);
        var schema = ReadRequiredLong(model, "schema", errors);
        var providerId = ReadRequiredString(
            model,
            "provider",
            "manifest",
            errors);
        var assetTables = ReadAssets(model, errors);
        IAssetProvider? provider = null;
        if (!string.IsNullOrWhiteSpace(providerId)) {
            try {
                provider = providers.Get(providerId);
            } catch (AssetManifestException error) {
                errors.AddRange(error.Errors);
            }
        }

        var assets = new List<AssetEntry>();
        if (assetTables is not null) {
            for (var index = 0; index < assetTables.Count; index++) {
                var prefix = $"assets[{index}]";
                var table = assetTables[index];
                ValidateUnknownKeys(table, AssetKeys, prefix, errors);
                var commit = ReadRequiredString(
                    table,
                    "commit",
                    prefix,
                    errors);
                var upstreamPath = ReadRequiredString(
                    table,
                    "path",
                    prefix,
                    errors);
                var output = ReadRequiredString(
                    table,
                    "output",
                    prefix,
                    errors);
                var sha256 = ReadRequiredString(
                    table,
                    "sha256",
                    prefix,
                    errors);
                var purpose = ReadRequiredString(
                    table,
                    "purpose",
                    prefix,
                    errors);
                var optional = ReadRequiredBool(
                    table,
                    "optional",
                    prefix,
                    errors);
                if (commit is not null &&
                    upstreamPath is not null &&
                    output is not null &&
                    sha256 is not null &&
                    purpose is not null &&
                    optional is { } optionalValue) {
                    ValidateCommon(
                        commit,
                        upstreamPath,
                        output,
                        sha256,
                        purpose,
                        prefix,
                        errors);
                    var entry = new AssetEntry(
                        commit,
                        upstreamPath,
                        output,
                        sha256,
                        purpose,
                        optionalValue);
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

        if (assets.Count == 0 && (assetTables?.Count ?? 0) == 0) {
            errors.Add("manifest: at least one asset is required.");
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

    private static TomlTableArray? ReadAssets(
        TomlTable model,
        ICollection<string> errors
    ) {
        if (!model.TryGetValue("assets", out var value)) {
            errors.Add("manifest: missing required field 'assets'.");
            return null;
        }

        if (value is TomlTableArray tables) {
            return tables;
        }

        errors.Add("manifest: assets must be an array of tables.");
        return null;
    }

    private static void ValidateUnknownKeys(
        TomlTable table,
        IReadOnlyCollection<string>? allowed,
        string prefix,
        ICollection<string> errors
    ) {
        foreach (var key in table.Keys) {
            if (allowed is null ||
                !allowed.Contains(key)) {
                errors.Add($"{prefix}: unknown field '{key}'.");
            }
        }
    }

    private static string? ReadRequiredString(
        TomlTable table,
        string key,
        string prefix,
        ICollection<string> errors
    ) {
        if (!table.TryGetValue(key, out var value) ||
            value is not string text) {
            errors.Add(
                $"{prefix}: field '{key}' must be a string.");
            return null;
        }

        return text;
    }

    private static long? ReadRequiredLong(
        TomlTable table,
        string key,
        ICollection<string> errors
    ) {
        if (!table.TryGetValue(key, out var value) ||
            value is not long number) {
            errors.Add($"manifest: field '{key}' must be an integer.");
            return null;
        }

        return number;
    }

    private static bool? ReadRequiredBool(
        TomlTable table,
        string key,
        string prefix,
        ICollection<string> errors
    ) {
        if (!table.TryGetValue(key, out var value) ||
            value is not bool flag) {
            errors.Add($"{prefix}: field '{key}' must be a boolean.");
            return null;
        }

        return flag;
    }
}
