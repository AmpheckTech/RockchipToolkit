namespace Ampheck.RockchipToolkit;

public sealed record ToolkitTransferLimits {
    private const long MiB = 1024L * 1024L;

    public long MaximumInputBytes { get; init; } =
        32 * 1024 * MiB;
    public long MaximumExpandedBytes { get; init; } =
        64 * 1024 * MiB;
    public int MaximumArchiveEntries { get; init; } = 4096;
    public int MaximumArchivePathLength { get; init; } = 1024;

    public static ToolkitTransferLimits Default { get; } = new();

    public void Validate() {
        if (MaximumInputBytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumInputBytes),
                "The maximum input size must be positive.");
        }

        if (MaximumExpandedBytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumExpandedBytes),
                "The maximum expanded size must be positive.");
        }

        if (MaximumArchiveEntries <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumArchiveEntries),
                "The maximum archive entry count must be positive.");
        }

        if (MaximumArchivePathLength <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumArchivePathLength),
                "The maximum archive path length must be positive.");
        }
    }
}
