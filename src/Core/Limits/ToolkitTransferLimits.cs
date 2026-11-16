namespace Ampheck.RockchipToolkit.Core.Limits;

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
        if (MaximumInputBytes <= 0 ||
            MaximumExpandedBytes <= 0 ||
            MaximumArchiveEntries <= 0 ||
            MaximumArchivePathLength <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(ToolkitTransferLimits),
                "Toolkit transfer limits must be positive.");
        }
    }

}
