namespace Ampheck.RockchipToolkit.Rpc;

public enum ToolkitFrameKind : byte {
    Request = 1,
    Response = 2,
    Notify = 3,
    Data = 4,
    AttachmentEnd = 5,
    Cancel = 6,
    Close = 7
}
