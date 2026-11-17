using System.Diagnostics;
using System.Globalization;

namespace Ampheck.RockchipToolkit.Cli.Terminal;

internal sealed class ProgressBar : IProgress<long>, IDisposable {
    private static readonly TimeSpan RefreshInterval =
        TimeSpan.FromMilliseconds(100);

    private readonly ConsoleRenderer _renderer;
    private readonly string _label;
    private readonly ProgressRateTracker? _rateTracker;
    private long _total;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _value;
    private TimeSpan _lastRefresh;
    private double? _speed;
    private bool _completed;
    private bool _disposed;

    public ProgressBar(
        ConsoleRenderer renderer,
        string label,
        long total,
        bool trackRate = true
    ) {
        _renderer = renderer;
        _label = label;
        _total = total;
        _rateTracker = trackRate
            ? new ProgressRateTracker()
            : null;
        Report(0);
    }

    public void Report(long value) =>
        Report(value, speed: null);

    public void Report(
        long value,
        double? speed
    ) {
        _value = Math.Clamp(value, 0, _total);
        if (_disposed || !_renderer.IsInteractive) {
            return;
        }

        var now = _stopwatch.Elapsed;
        _speed = speed ?? _rateTracker?.Update(_value);
        _completed = _value >= _total;
        if (_completed) {
            return;
        }

        if (now - _lastRefresh < RefreshInterval) {
            return;
        }

        _lastRefresh = now;
        _renderer.SetProgress(BuildLine(_value));
    }

    public void UpdateTotal(long total) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(total);
        _total = total;
        _lastRefresh = TimeSpan.Zero;
        Report(_value);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        if (_completed) {
            _renderer.CompleteProgress(BuildLine(_total));
        } else {
            _renderer.ClearProgress();
        }
    }

    private string BuildLine(long value) {
        var ratio = _total == 0
            ? 1d
            : (double)value / _total;
        var percent = Math.Clamp(
            (int)Math.Round(ratio * 100),
            0,
            100);
        var width = Math.Max(_renderer.Width - 1, 39);
        var label = Truncate(
            _label,
            16);
        var barWidth = Math.Clamp(
            width - label.Length - 36,
            8,
            40);
        var filled = Math.Clamp(
            (int)Math.Round(ratio * barWidth),
            0,
            barWidth);
        var empty = barWidth - filled;
        if (!_renderer.IsInteractive) {
            return string.Empty;
        }

        return
            $"{label} |" +
            new string('#', filled) +
            new string('-', empty) +
            $"| {percent,3}% " +
            $"{FormatBytes(value),8}/{FormatBytes(_total),8} " +
            FormatSpeed();
    }

    private string FormatSpeed() {
        return _speed is > 0
            ? $"{FormatBytes((long)_speed.Value),7}/s"
            : "     --/s";
    }

    private static string Truncate(
        string value,
        int maximumLength
    ) {
        if (value.Length <= maximumLength) {
            return value;
        }

        return value[..(maximumLength - 1)] + "~";
    }

    internal static string FormatBytes(long value) {
        var absolute = Math.Max(value, 0);
        if (absolute >= 1L << 30) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{absolute / (double)(1L << 30):F2}G");
        }

        if (absolute >= 1L << 20) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{absolute / (double)(1L << 20):F1}M");
        }

        if (absolute >= 1L << 10) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{absolute / (double)(1L << 10):F0}K");
        }

        return $"{absolute}B";
    }
}
