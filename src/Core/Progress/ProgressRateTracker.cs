using System.Diagnostics;

namespace Ampheck.RockchipToolkit.Core.Progress;

public sealed class ProgressRateTracker {
    private static readonly TimeSpan MinimumSampleInterval =
        TimeSpan.FromMilliseconds(250);
    private const double SmoothingSeconds = 0.75;

    private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
    private long? _sampleTimestamp;
    private long _sampleValue;
    private double? _rate;

    public ProgressMetrics Update(
        long current,
        long? total
    ) {
        var elapsed = Stopwatch.GetElapsedTime(_startedTimestamp);
        if (_sampleTimestamp is null) {
            _sampleTimestamp = Stopwatch.GetTimestamp();
            _sampleValue = current;
            return CreateMetrics(current, total, elapsed);
        }

        var sampleElapsed = Stopwatch.GetElapsedTime(
            _sampleTimestamp.Value);
        if (sampleElapsed >= MinimumSampleInterval) {
            var delta = current - _sampleValue;
            if (delta < 0) {
                _rate = null;
            } else if (delta > 0) {
                var currentRate =
                    delta / sampleElapsed.TotalSeconds;
                _rate = _rate is null
                    ? currentRate
                    : _rate + (currentRate - _rate) *
                        (1 - Math.Exp(
                            -sampleElapsed.TotalSeconds /
                            SmoothingSeconds));
            }

            _sampleTimestamp = Stopwatch.GetTimestamp();
            _sampleValue = current;
        }

        return CreateMetrics(current, total, elapsed);
    }

    private ProgressMetrics CreateMetrics(
        long current,
        long? total,
        TimeSpan elapsed
    ) {
        TimeSpan? remaining = null;
        if (_rate is > 0 && total > current) {
            var seconds = (total.Value - current) / _rate.Value;
            if (seconds < TimeSpan.MaxValue.TotalSeconds) {
                remaining = TimeSpan.FromSeconds(seconds);
            }
        }

        return new ProgressMetrics(
            elapsed,
            _rate,
            remaining);
    }
}
