using System.Reflection;

namespace Ampheck.Adb.Cli;

internal static class AdbCliVersion {
    public static string Current { get; } = GetVersion();

    private static string GetVersion() {
        var informational = typeof(AdbCliVersion)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (informational is null) {
            return typeof(AdbCliVersion)
                .Assembly
                .GetName()
                .Version?
                .ToString(3)
                ?? "unknown";
        }

        var separator = informational.IndexOf('+');
        return separator < 0
            ? informational
            : informational[..separator];
    }
}
