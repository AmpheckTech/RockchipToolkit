namespace Ampheck.RockchipToolkit.Core.Progress;

public sealed record ProgressMetrics(
    TimeSpan Elapsed,
    double? RatePerSecond,
    TimeSpan? Remaining
);
