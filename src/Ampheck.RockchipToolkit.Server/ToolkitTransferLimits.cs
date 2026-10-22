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

    public static ToolkitTransferLimits FromEnvironment() {
        var defaults = Default;
        return new ToolkitTransferLimits {
            MaximumAttachmentBytes = ReadLong(
                "ARKT_MAX_ATTACHMENT_BYTES",
                defaults.MaximumAttachmentBytes),
            MaximumRequestBytes = ReadLong(
                "ARKT_MAX_REQUEST_BYTES",
                defaults.MaximumRequestBytes),
            MaximumExpandedBytes = ReadLong(
                "ARKT_MAX_EXPANDED_BYTES",
                defaults.MaximumExpandedBytes),
            MaximumAttachments = ReadInt(
                "ARKT_MAX_ATTACHMENTS",
                defaults.MaximumAttachments),
            MaximumArchiveEntries = ReadInt(
                "ARKT_MAX_ARCHIVE_ENTRIES",
                defaults.MaximumArchiveEntries),
            MaximumArchivePathLength = ReadInt(
                "ARKT_MAX_ARCHIVE_PATH_LENGTH",
                defaults.MaximumArchivePathLength)
        };
    }

    private static long ReadLong(
        string name,
        long fallback) {
        return long.TryParse(
                Environment.GetEnvironmentVariable(name),
                out var value)
            ? value
            : fallback;
    }

    private static int ReadInt(
        string name,
        int fallback) {
        return int.TryParse(
                Environment.GetEnvironmentVariable(name),
                out var value)
            ? value
            : fallback;
    }
}
