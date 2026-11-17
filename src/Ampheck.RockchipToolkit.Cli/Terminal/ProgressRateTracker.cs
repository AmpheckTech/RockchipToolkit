using System.Diagnostics;

namespace Ampheck.RockchipToolkit.Cli.Terminal;

internal sealed class ProgressRateTracker {
    private static readonly TimeSpan MinimumSampleInterval =
        TimeSpan.FromMilliseconds(250);
    private const double SmoothingSeconds = 0.75;

    private long? _sampleTimestamp;
    private long _sampleValue;
    private double? _rate;

    public double? Update(long current) {
        if (_sampleTimestamp is null) {
            _sampleTimestamp = Stopwatch.GetTimestamp();
            _sampleValue = current;
            return _rate;
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

        return _rate;
    }
}
