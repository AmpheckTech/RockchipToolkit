using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitDiscoveryCommandRunner {
    public static async Task<int> RunAsync(
        TimeSpan timeout) {
        var servers = await ToolkitDiscoveryClient.DiscoverAsync(
                timeout)
            .ConfigureAwait(false);
        if (servers.Count == 0) {
            Console.WriteLine("No toolkit servers found.");
            return 0;
        }

        Console.WriteLine(
            "GUID                                 " +
            "HOSTNAME             ENDPOINT               AUTH");
        foreach (var server in servers) {
            Console.WriteLine(
                $"{server.InstanceId,-36} " +
                $"{server.HostName,-20} " +
                $"{server.Endpoint,-22} " +
                $"{(server.TokenRequired ? "token" : "none")}");
        }

        return 0;
    }
}
