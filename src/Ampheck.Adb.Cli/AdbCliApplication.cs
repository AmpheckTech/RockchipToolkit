using System.CommandLine;
using Ampheck.Adb.Cli.Commands;

namespace Ampheck.Adb.Cli;

internal static class AdbCliApplication {
    public static async Task<int> RunAsync(string[] args) {
        var options = new AdbCliOptions();
        var root = new RootCommand(
            "Connect to Android devices and manage an ADB server.");
        options.AddTo(root);
        ServerCommands.AddTo(root, options);
        DeviceCommands.AddTo(root, options);
        FileCommands.AddTo(root, options);
        ForwardCommands.AddTo(root, options);
        return await root.Parse(args).InvokeAsync()
            .ConfigureAwait(false);
    }
}
