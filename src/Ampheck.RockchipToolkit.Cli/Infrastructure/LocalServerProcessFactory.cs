using System.Diagnostics;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class LocalServerProcessFactory {
    public static ProcessStartInfo Create(
        bool verbose,
        LocalServerPaths paths) {
        var executable = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "The toolkit executable path is unavailable.");
        var frameworkDependent = Path
            .GetFileNameWithoutExtension(executable)
            .Equals(
                "dotnet",
                StringComparison.OrdinalIgnoreCase);
        var launchExecutable = executable;
        var launchArguments = new List<string>();
        if (frameworkDependent) {
            launchArguments.Add(
                Environment.GetCommandLineArgs()[0]);
        }

        launchArguments.Add("--daemon");
        launchArguments.Add("--log");
        launchArguments.Add(paths.LogPath);
        launchArguments.Add("--state");
        launchArguments.Add(paths.StatePath);
        launchArguments.Add("--");
        launchArguments.Add("server");
        launchArguments.Add("nodaemon");
        launchArguments.Add("--listen");
        launchArguments.Add(ToolkitServerDefaults.Endpoint);
        if (verbose) {
            launchArguments.Add("--verbose");
        }

        var startInfo = new ProcessStartInfo {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        if (OperatingSystem.IsLinux() &&
            File.Exists("/usr/bin/setsid")) {
            startInfo.FileName = "/usr/bin/setsid";
            startInfo.ArgumentList.Add(launchExecutable);
        } else {
            startInfo.FileName = launchExecutable;
        }

        foreach (var argument in launchArguments) {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
