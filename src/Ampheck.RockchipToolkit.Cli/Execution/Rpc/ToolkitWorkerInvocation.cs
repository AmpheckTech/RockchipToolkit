using System.Globalization;
using Ampheck.RockchipToolkit.Server;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed record ToolkitWorkerInvocation(
    string[] Arguments,
    bool IsWorker,
    ToolkitTransferLimits Limits) {
    private const string Marker = "--worker";
    private const string Separator = "--";

    public static ToolkitWorkerInvocation Parse(string[] args) {
        if (args.Length == 0 ||
            !args[0].Equals(Marker, StringComparison.Ordinal)) {
            return new ToolkitWorkerInvocation(
                args,
                IsWorker: false,
                ToolkitTransferLimits.Default);
        }

        var separator = Array.IndexOf(args, Separator, 1);
        if (separator < 0) {
            throw new ArgumentException(
                "The worker launch context has no command separator.");
        }

        var defaults = ToolkitTransferLimits.Default;
        long attachmentBytes = defaults.MaximumAttachmentBytes;
        long requestBytes = defaults.MaximumRequestBytes;
        long expandedBytes = defaults.MaximumExpandedBytes;
        var attachments = defaults.MaximumAttachments;
        var archiveEntries = defaults.MaximumArchiveEntries;
        var archivePathLength = defaults.MaximumArchivePathLength;
        for (var index = 1; index < separator; index++) {
            var option = args[index];
            var value = ReadValue(
                args,
                ref index,
                separator,
                option);
            switch (option) {
                case "--attachment-bytes":
                    attachmentBytes = ParseLong(value, option);
                    break;
                case "--request-bytes":
                    requestBytes = ParseLong(value, option);
                    break;
                case "--expanded-bytes":
                    expandedBytes = ParseLong(value, option);
                    break;
                case "--attachments":
                    attachments = ParseInt(value, option);
                    break;
                case "--archive-entries":
                    archiveEntries = ParseInt(value, option);
                    break;
                case "--archive-path-length":
                    archivePathLength = ParseInt(value, option);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown worker launch argument '{option}'.");
            }
        }

        var limits = new ToolkitTransferLimits {
            MaximumAttachmentBytes = attachmentBytes,
            MaximumRequestBytes = requestBytes,
            MaximumExpandedBytes = expandedBytes,
            MaximumAttachments = attachments,
            MaximumArchiveEntries = archiveEntries,
            MaximumArchivePathLength = archivePathLength
        };
        limits.Validate();
        return new ToolkitWorkerInvocation(
            args[(separator + 1)..],
            IsWorker: true,
            limits);
    }

    private static string ReadValue(
        string[] args,
        ref int index,
        int end,
        string option) {
        if (++index >= end) {
            throw new ArgumentException(
                $"Worker launch option {option} requires a value.");
        }

        return args[index];
    }

    private static long ParseLong(
        string value,
        string option) {
        return long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"Worker launch option {option} has invalid value " +
                $"'{value}'.");
    }

    private static int ParseInt(
        string value,
        string option) {
        return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"Worker launch option {option} has invalid value " +
                $"'{value}'.");
    }
}
