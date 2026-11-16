using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Terminal;

internal sealed class OperationProgressDisplay(
    ConsoleRenderer console
) : IProgress<OperationProgress>, IDisposable {
    private ProgressBar? _progress;
    private string? _label;
    private long _total;

    public void Report(OperationProgress value) {
        switch (value.Kind) {
            case "status":
                CompleteProgress();
                console.WriteStatus(value.Message);
                break;
            case "stdout":
                CompleteProgress();
                Console.WriteLine(value.Message);
                break;
            case "progress":
                UpdateProgress(value.Message, value.Progress);
                break;
        }
    }

    public void Complete() => CompleteProgress();

    public void Dispose() => CompleteProgress();

    private void UpdateProgress(
        string label,
        TaskProgress? value
    ) {
        if (value is null || value.Total <= 0) {
            return;
        }

        if (_progress is null ||
            !string.Equals(_label, label, StringComparison.Ordinal)) {
            CompleteProgress();
            _label = label;
            _total = value.Total;
            _progress = new ProgressBar(
                console,
                label,
                _total);
        } else if (_total != value.Total) {
            _total = value.Total;
            _progress.UpdateTotal(_total);
        }

        _progress.Report(value.Current);
    }

    private void CompleteProgress() {
        _progress?.Dispose();
        _progress = null;
        _label = null;
        _total = 0;
    }
}
