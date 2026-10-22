using System.Reflection;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class ProductVersion {
    private const int RevisionLength = 12;

    public static string Format(string product, Assembly assembly) {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = informational ??
            assembly.GetName().Version?.ToString(3) ??
            "unknown";
        var separator = version.IndexOf('+');
        if (separator < 0 ||
            separator == version.Length - 1) {
            return $"{product} {version}";
        }

        var revision = version[(separator + 1)..];
        if (revision.Length > RevisionLength) {
            revision = revision[..RevisionLength];
        }

        return
            $"{product} {version[..separator]}-" +
            revision;
    }
}
