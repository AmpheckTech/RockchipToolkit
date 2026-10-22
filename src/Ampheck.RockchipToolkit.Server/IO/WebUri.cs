using System.Diagnostics.CodeAnalysis;

namespace Ampheck.RockchipToolkit.Server.IO;

internal static class WebUri {
    public static bool TryGetHttp(
        string location,
        [NotNullWhen(true)] out Uri? uri) {
        if (Uri.TryCreate(location, UriKind.Absolute, out var candidate) &&
            candidate.Scheme is "http" or "https") {
            uri = candidate;
            return true;
        }

        if (location.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase) ||
            location.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Invalid HTTP or HTTPS URL.");
        }

        uri = null;
        return false;
    }

    public static Uri AsDirectory(Uri uri) {
        if (uri.AbsolutePath.EndsWith(
                "/",
                StringComparison.Ordinal)) {
            return uri;
        }

        var builder = new UriBuilder(uri);
        builder.Path = builder.Path.TrimEnd('/') + "/";
        return builder.Uri;
    }

    public static string GetDisplayAuthority(Uri uri) {
        return uri.IsDefaultPort
            ? uri.Host
            : $"{uri.Host}:{uri.Port}";
    }
}
