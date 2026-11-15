using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record CliInvocation(
    string Command,
    bool RequiresServer,
    Func<CliCommandContext, CancellationToken, Task<int>> Execute);

internal sealed record CliCommandContext(
    bool Verbose,
    string? StatePath,
    ILoggerFactory LoggerFactory,
    ILogger Logger,
    ConsoleRenderer Console,
    ToolkitEndpoint? Endpoint,
    CancellationToken CancellationToken) {
    public CliRpcSession OpenRpcSession() {
        var endpoint = Endpoint ??
            throw new InvalidOperationException(
                "The command requires a toolkit server endpoint.");
        return new CliRpcSession(
            endpoint,
            Logger,
            Console);
    }
}
