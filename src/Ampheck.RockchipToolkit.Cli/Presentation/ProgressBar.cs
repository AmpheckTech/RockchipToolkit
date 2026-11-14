using System.Diagnostics;
using System.Globalization;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class ProgressBar : IProgress<long>, IDisposable {
    private static readonly TimeSpan RefreshInterval =
        TimeSpan.FromMilliseconds(100);
    private const double SpeedWindowSeconds = 1;

    private readonly ConsoleRenderer _renderer;
    private readonly string _label;
    private long _total;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _value;
    private long _lastSpeedValue;
    private TimeSpan _lastRefresh;
    private TimeSpan _lastSpeedRefresh;
    private double _speed;
    private bool _completed;
    private bool _disposed;

    public ProgressBar(
        ConsoleRenderer renderer,
        string label,
        long total) {
        _renderer = renderer;
        _label = label;
        _total = total;
        Report(0);
    }

    public void Report(long value) {
        _value = Math.Clamp(value, 0, _total);
        if (_disposed || !_renderer.IsInteractive) {
            return;
        }

        var now = _stopwatch.Elapsed;
        UpdateSpeed(now);
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
            $"{FormatBytes((long)_speed),7}/s";
    }

    private void UpdateSpeed(TimeSpan now) {
        var elapsed =
            (now - _lastSpeedRefresh).TotalSeconds;
        var delta = _value - _lastSpeedValue;
        if (delta < 0) {
            _speed = 0;
        } else if (elapsed > 0 && delta > 0) {
            var current = delta / elapsed;
            if (_speed <= 0) {
                _speed = current;
            } else {
                var weight = Math.Clamp(
                    elapsed / SpeedWindowSeconds,
                    0,
                    1);
                _speed += (current - _speed) * weight;
            }
        }

        _lastSpeedValue = _value;
        _lastSpeedRefresh = now;
    }

    private static string Truncate(
        string value,
        int maximumLength) {
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
