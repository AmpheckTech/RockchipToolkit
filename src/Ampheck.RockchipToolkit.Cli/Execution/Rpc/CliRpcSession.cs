using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class CliRpcSession : IAsyncDisposable {
    private readonly ToolkitEventPrinter _printer;

    public CliRpcSession(
        ToolkitEndpoint endpoint,
        ILogger logger,
        ConsoleRenderer console) {
        Client = new ToolkitRpcClient(
            endpoint,
            Environment.GetEnvironmentVariable("ARKT_SERVER_TOKEN"),
            Environment.GetEnvironmentVariable("ARKT_CLIENT_NAME"),
            ToolkitFileAccess.Parse(
                Environment.GetEnvironmentVariable("ARKT_FILE_MODE")));
        Payload = new ToolkitPayloadPlan(Client, logger, console);
        _printer = new ToolkitEventPrinter(console);
    }

    public ToolkitRpcClient Client { get; }
    public ToolkitPayloadPlan Payload { get; }

    public void Report(TaskEventNotification value) {
        _printer.Write(value);
    }

    public async ValueTask DisposeAsync() {
        _printer.Dispose();
        await Payload.DisposeAsync().ConfigureAwait(false);
        await Client.DisposeAsync().ConfigureAwait(false);
    }
}
