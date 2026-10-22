namespace Ampheck.RockchipToolkit.Rpc;

public enum ToolkitOpcode : ushort {
    Hello = 0x0001,
    Call = 0x0002,
    Cancel = 0x0003,
    CloseChannel = 0x0004,
    Log = 0x0100,
    Progress = 0x0101,
    Unsupported = 0x7FFF
}
