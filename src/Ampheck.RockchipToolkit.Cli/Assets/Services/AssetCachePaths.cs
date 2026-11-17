namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal sealed class AssetCachePaths {
    private const string EnvironmentVariable = "ARKT_ASSETS_DIR";

    public AssetCachePaths(string root) {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public static AssetCachePaths Create() {
        var configured = Environment.GetEnvironmentVariable(
            EnvironmentVariable);
        return new AssetCachePaths(
            string.IsNullOrWhiteSpace(configured)
                ? DefaultRoot()
                : configured);
    }

    public string GetAssetPath(string sha256) {
        ValidateHash(sha256);
        return Path.Combine(
            Root,
            "sha256",
            sha256[..2],
            sha256[2..4],
            sha256);
    }

    public string GetPartPath(string sha256) {
        ValidateHash(sha256);
        return GetAssetPath(sha256) + ".part";
    }

    public string GetLockPath(string sha256) {
        ValidateHash(sha256);
        return Path.Combine(Root, "locks", $"{sha256}.lock");
    }

    public string GetReferenceDirectory() =>
        Path.Combine(Root, "references");

    private static string DefaultRoot() {
        if (OperatingSystem.IsWindows()) {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Ampheck",
                "arkt",
                "assets");
        }

        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS()) {
            return Path.Combine(
                home,
                "Library",
                "Application Support",
                "Ampheck",
                "arkt",
                "assets");
        }

        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(
            string.IsNullOrWhiteSpace(dataHome) ||
                !Path.IsPathRooted(dataHome)
                ? Path.Combine(home, ".local", "share")
                : dataHome,
            "arkt",
            "assets");
    }

    private static void ValidateHash(string sha256) {
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                sha256,
                "^[0-9a-f]{64}$")) {
            throw new ArgumentException(
                "The asset hash must be 64 lowercase hexadecimal " +
                "characters.",
                nameof(sha256));
        }
    }
}
