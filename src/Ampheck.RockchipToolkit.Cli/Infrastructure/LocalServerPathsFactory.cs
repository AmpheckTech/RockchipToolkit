namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class LocalServerPathsFactory {
    public static LocalServerPaths Create() {
        var runtimeDirectory = Environment.GetEnvironmentVariable(
            "XDG_RUNTIME_DIR");
        var directory = !string.IsNullOrWhiteSpace(runtimeDirectory)
            ? Path.Combine(
                runtimeDirectory,
                "ampheck-rockchip-toolkit")
            : Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Ampheck",
                "RockchipToolkit");
        return new LocalServerPaths(
            directory,
            Path.Combine(directory, "server.state"),
            Path.Combine(directory, "server.log"));
    }
}
