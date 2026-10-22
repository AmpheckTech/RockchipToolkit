using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class ToolkitEventPrinter(
    ConsoleRenderer console) : IDisposable {
    private ProgressBar? _progress;
    private string? _progressLabel;
    private long _progressTotal;
    private string? _activity;

    public void Write(ToolkitEvent value) {
        if (value.Source == "progress" &&
            value.Current is { } current &&
            value.Total is { } total) {
            if (_activity is not null) {
                _activity = null;
                console.CompleteActivity();
            }

            ShowProgress(
                value.Message,
                current,
                total);
            return;
        }

        if (value.Source == "status") {
            if (_activity is not null) {
                console.CompleteActivity(
                    console.IsInteractive
                        ? _activity
                        : null);
            }

            _activity = value.Message;
            console.StartActivity(value.Message);
            return;
        }

        if (value.Source == "success") {
            console.CompleteActivity();
            _activity = null;
            console.WriteStatus(
                value.Message,
                complete: true);
            return;
        }

        if (_activity is not null) {
            console.CompleteActivity(
                console.IsInteractive &&
                value.Source != "stderr"
                    ? _activity
                    : null);
            _activity = null;
        }

        if (value.Source == "stderr") {
            Console.Error.WriteLine(value.Message);
        } else {
            Console.WriteLine(value.Message);
        }
    }

    public void Dispose() {
        _progress?.Dispose();
        _progress = null;
        console.CompleteActivity();
        _activity = null;
    }

    private void ShowProgress(
        string label,
        long current,
        long total) {
        if (total <= 0) {
            return;
        }

        if (_progress is null ||
            _progressLabel != label ||
            _progressTotal != total) {
            _progress?.Dispose();
            _progress = new ProgressBar(
                console,
                label,
                total);
            _progressLabel = label;
            _progressTotal = total;
        }

        _progress.Report(current);
        if (current < total) {
            return;
        }

        _progress.Dispose();
        _progress = null;
        _progressLabel = null;
    }
}
