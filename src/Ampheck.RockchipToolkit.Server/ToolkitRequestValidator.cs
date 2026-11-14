namespace Ampheck.RockchipToolkit.Server;

/// <summary>Validates remote request inputs and attachment limits before execution.</summary>
internal static class ToolkitRequestValidator {
    public static void Validate(
        ToolkitRequest request,
        ToolkitTransferLimits limits) {
        if (request.Attachments
            .Select(attachment => attachment.Role)
            .Distinct()
            .Count() != request.Attachments.Count) {
            throw new InvalidDataException(
                "The remote request contains duplicate attachment roles.");
        }

        if (request.Operation == ToolkitOperation.Flash) {
            ValidateInput(
                "firmware",
                request.SourceLocation,
                request.Source,
                ToolkitAttachmentRole.Source,
                request.Attachments);
        }

        if (request.Operation == ToolkitOperation.Loader) {
            ValidateInput(
                "loader",
                request.FileLocation,
                request.File,
                ToolkitAttachmentRole.Loader,
                request.Attachments);
        }

        if (request.Operation == ToolkitOperation.Write) {
            ValidateInput(
                "write input",
                request.FileLocation,
                request.File,
                ToolkitAttachmentRole.Input,
                request.Attachments);
        }

        if (request.Loader is not null) {
            ValidateInput(
                "loader",
                request.LoaderLocation,
                request.Loader,
                ToolkitAttachmentRole.Loader,
                request.Attachments);
        }

        ValidateAttachmentLimits(request.Attachments, limits);
    }

    private static void ValidateAttachmentLimits(
        IReadOnlyList<ToolkitAttachment> attachments,
        ToolkitTransferLimits limits) {
        if (attachments.Count > limits.MaximumAttachments) {
            throw new InvalidDataException(
                $"The request contains more than " +
                $"{limits.MaximumAttachments} attachments.");
        }

        long total = 0;
        foreach (var attachment in attachments) {
            if (!Enum.IsDefined(attachment.Role) ||
                !Enum.IsDefined(attachment.Kind) ||
                string.IsNullOrWhiteSpace(attachment.FileName) ||
                attachment.FileName.Length > 255 ||
                attachment.FileName.Any(char.IsControl) ||
                attachment.FileName is "." or ".." ||
                attachment.FileName.Contains('/') ||
                attachment.FileName.Contains('\\')) {
                throw new InvalidDataException(
                    "The request contains an invalid attachment descriptor.");
            }

            if (attachment.Length < 0 ||
                attachment.Length > limits.MaximumAttachmentBytes) {
                throw new InvalidDataException(
                    $"Attachment '{attachment.FileName}' exceeds the " +
                    $"configured size limit.");
            }

            if (attachment.Length >
                limits.MaximumRequestBytes - total) {
                throw new InvalidDataException(
                    "The request attachments exceed the configured " +
                    "total size limit.");
            }

            total += attachment.Length;
        }
    }

    private static void ValidateInput(
        string description,
        ToolkitInputKind location,
        string? value,
        ToolkitAttachmentRole role,
        IReadOnlyList<ToolkitAttachment> attachments) {
        if (location == ToolkitInputKind.LocalPath) {
            throw new InvalidDataException(
                $"A remote request cannot use a server-local " +
                $"{description} path.");
        }

        if (location == ToolkitInputKind.Attachment) {
            var attachment = attachments.FirstOrDefault(
                item => item.Role == role);
            if (attachment is null) {
                throw new InvalidDataException(
                    $"The remote request declares an attachment-backed " +
                    $"{description}, but no matching attachment is present.");
            }

            if (role != ToolkitAttachmentRole.Source &&
                attachment.Kind != ToolkitAttachmentKind.File) {
                throw new InvalidDataException(
                    $"The {description} attachment must be a file.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidDataException(
                $"The remote request contains no {description}.");
        }
    }
}
