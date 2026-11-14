namespace Ampheck.RockchipToolkit.Images;

internal sealed class RockchipImageProgressReporter(
    IProgress<RockchipImageProgress>? progress) {
    private long _current;
    private long _total;

    public void AddTotal(long bytes) {
        if (bytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        if (bytes == 0) {
            return;
        }

        _total = checked(_total + bytes);
        Report();
    }

    public void Advance(long bytes) {
        if (bytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(bytes));
        }

        if (bytes == 0) {
            return;
        }

        _current = checked(_current + bytes);
        Report();
    }

    public void Complete() {
        if (_total == 0) {
            return;
        }

        _current = _total;
        Report();
    }

    private void Report() {
        progress?.Report(new RockchipImageProgress(
            _current,
            _total));
    }
}
