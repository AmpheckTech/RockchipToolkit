using Ampheck.RockchipToolkit;

namespace Ampheck.RockchipToolkit.Cli.Terminal;

internal sealed class OperationProgressDisplay(
    ConsoleRenderer console
) : IProgress<OperationProgress>, IDisposable {
    private ProgressBar? _progress;
    private ToolkitOperationStage? _stage;
    private string? _item;
    private long _total;

    public void Report(OperationProgress value) {
        if (value.Total <= 0) {
            CompleteProgress();
            var label = value.Item is null
                ? StageLabel(value.Stage)
                : $"{StageLabel(value.Stage)}: {value.Item}";
            console.WriteStatus(label);
            return;
        }

        UpdateProgress(value);
    }

    public void Complete() => CompleteProgress();

    public void Dispose() => CompleteProgress();

    private void UpdateProgress(
        OperationProgress value
    ) {
        if (_progress is null ||
            _stage != value.Stage ||
            !string.Equals(_item, value.Item, StringComparison.Ordinal)) {
            CompleteProgress();
            _stage = value.Stage;
            _item = value.Item;
            _total = value.Total;
            _progress = new ProgressBar(
                console,
                value.Item ?? StageLabel(value.Stage),
                _total);
        } else if (_total != value.Total) {
            _total = value.Total;
            _progress.UpdateTotal(_total);
        }

        _progress.Report(value.Completed);
    }

    private void CompleteProgress() {
        _progress?.Dispose();
        _progress = null;
        _stage = null;
        _item = null;
        _total = 0;
    }

    private static string StageLabel(ToolkitOperationStage stage) =>
        stage switch {
            ToolkitOperationStage.Preparing => "Preparing",
            ToolkitOperationStage.Downloading => "Downloading",
            ToolkitOperationStage.LoadingLoader => "Loading loader",
            ToolkitOperationStage.Reading => "Reading",
            ToolkitOperationStage.Erasing => "Erasing",
            ToolkitOperationStage.Writing => "Writing",
            ToolkitOperationStage.Verifying => "Verifying",
            ToolkitOperationStage.Resetting => "Resetting",
            _ => stage.ToString()
        };
}
