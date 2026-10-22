namespace Ampheck.RockchipToolkit.Rpc;

public enum ToolkitStatus : ushort {
    Ok = 0,
    InvalidRequest = 1,
    Unauthorized = 2,
    UnsupportedProtocol = 3,
    UnsupportedMethod = 4,
    Busy = 5,
    Cancelled = 6,
    Internal = 7
}
