using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Cli.Terminal;

internal sealed class ImageProgressDisplay(
    ConsoleRenderer console,
    string label
) : IProgress<RockchipImageProgress>, IDisposable {
    private ProgressBar? _progress;
    private long _total;

    public void Report(RockchipImageProgress value) {
        if (value.Total <= 0) {
            return;
        }

        if (_progress is null) {
            console.CompleteActivity();
            _progress = new ProgressBar(
                console,
                label,
                value.Total);
            _total = value.Total;
        } else if (_total != value.Total &&
            value.Current >= _total) {
            _progress.Dispose();
            var phaseStart = _total;
            _total = value.Total;
            _progress = new ProgressBar(
                console,
                label,
                value.Total - phaseStart);
            _progress.Report(value.Current - phaseStart);
            return;
        } else if (_total != value.Total) {
            _progress.UpdateTotal(value.Total);
            _total = value.Total;
        }

        _progress.Report(value.Current);
    }

    public void Complete() {
        _progress?.Dispose();
        _progress = null;
    }

    public void Dispose() {
        Complete();
    }
}
