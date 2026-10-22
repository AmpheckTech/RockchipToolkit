namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitAttachment(
    ToolkitAttachmentRole Role,
    ToolkitAttachmentKind Kind,
    string FileName,
    long Length);
