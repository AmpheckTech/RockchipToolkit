using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed class FirmwareFileContext(
    ILogger logger,
    Func<string, ValueTask> report,
    Func<long, long, ValueTask>? progress,
    ToolkitTransferLimits limits) {
    public ILogger Logger { get; } = logger;
    public Func<string, ValueTask> Report { get; } = report;
    public Func<long, long, ValueTask>? Progress { get; } = progress;
    public ToolkitTransferLimits Limits { get; } = limits;

    public ValueTask<InputFile> OpenInputAsync(
        string location,
        CancellationToken cancellationToken) {
        return InputFile.OpenAsync(
            location,
            this,
            cancellationToken);
    }

    public ValueTask<FlashInput> OpenFlashInputAsync(
        string location,
        CancellationToken cancellationToken) {
        return FlashInput.OpenAsync(
            location,
            this,
            cancellationToken);
    }

    public ValueTask<InputFile?> TryOpenSourceAsync(
        FirmwareSource source,
        string fileName,
        CancellationToken cancellationToken) {
        return source.TryOpenAsync(
            fileName,
            this,
            cancellationToken);
    }
}
