using System.Reflection;

namespace Ampheck.RockchipToolkit.Rpc;

public static class ToolkitVersion {
    public static string Current { get; } =
        GetVersion();

    private static string GetVersion() {
        var assembly = Assembly.GetEntryAssembly() ??
            typeof(ToolkitVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (informational is null) {
            return assembly.GetName().Version?.ToString(3) ??
                "0.0.0";
        }

        var separator = informational.IndexOf('+');
        return separator < 0
            ? informational
            : informational[..separator];
    }
}
