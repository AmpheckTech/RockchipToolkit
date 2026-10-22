namespace Ampheck.RockchipToolkit.Server;

public interface IToolkitExecutor {
    Task<ToolkitResult> ExecuteAsync(
        ToolkitRequest request,
        Func<ToolkitEvent, ValueTask> report,
        CancellationToken cancellationToken);
}
