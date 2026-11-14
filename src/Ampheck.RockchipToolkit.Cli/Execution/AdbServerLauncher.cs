using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class AdbServerLauncher {
    public static async Task EnsureStartedAsync(
        ToolkitEndpoint? endpoint,
        CancellationToken cancellationToken = default) {
        if (endpoint is null) {
            return;
        }

        var token = Environment.GetEnvironmentVariable(
            "ARKT_SERVER_TOKEN");
        await using var client = new ToolkitClient(endpoint, token);
        var result = await client.ExecuteAsync(
                new ToolkitRequest {
                    Operation = ToolkitOperation.AdbServerEnsure
                },
                onEvent: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success) {
            throw new InvalidOperationException(
                result.Error ?? "Unable to start the official ADB server.");
        }
    }
}
