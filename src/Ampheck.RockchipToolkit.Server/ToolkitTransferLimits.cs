namespace Ampheck.RockchipToolkit.Server;

public sealed record ToolkitTransferLimits {
    private const long MiB = 1024L * 1024L;

    public long MaximumAttachmentBytes { get; init; } =
        4096 * MiB;
    public long MaximumRequestBytes { get; init; } =
        8192 * MiB;
    public long MaximumExpandedBytes { get; init; } =
        8192 * MiB;
    public int MaximumAttachments { get; init; } = 8;
    public int MaximumArchiveEntries { get; init; } = 4096;
    public int MaximumArchivePathLength { get; init; } = 1024;

    public static ToolkitTransferLimits Default { get; } = new();

    public void Validate() {
        if (MaximumAttachmentBytes <= 0 ||
            MaximumRequestBytes < MaximumAttachmentBytes ||
            MaximumExpandedBytes <= 0 ||
            MaximumAttachments <= 0 ||
            MaximumArchiveEntries <= 0 ||
            MaximumArchivePathLength <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(ToolkitTransferLimits),
                "Toolkit transfer limits must be positive, and the " +
                "request limit must cover one attachment.");
        }
    }

}
