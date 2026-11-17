namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal static class AssetPathValidator {
    public static string? ValidateRelativePath(
        string value,
        string field
    ) {
        if (string.IsNullOrWhiteSpace(value)) {
            return $"{field} must not be empty.";
        }

        if (value.Contains('\\')) {
            return $"{field} must use forward slashes.";
        }

        if (value.StartsWith('/') ||
            System.Text.RegularExpressions.Regex.IsMatch(
                value,
                "^[A-Za-z]:") ||
            Path.IsPathRooted(value)) {
            return $"{field} must be a relative path.";
        }

        var segments = value.Split('/');
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or "..")) {
            return $"{field} contains an invalid path segment.";
        }

        return null;
    }

    public static string ResolveUnderRoot(
        string root,
        string relativePath
    ) {
        var error = ValidateRelativePath(relativePath, "output");
        if (error is not null) {
            throw new InvalidDataException(error);
        }

        var fullRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(
            Path.Combine(
                fullRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
        var prefix = fullRoot.EndsWith(
                Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(
                prefix,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)) {
            throw new InvalidDataException(
                "The output path escapes the target directory.");
        }

        return path;
    }
}
