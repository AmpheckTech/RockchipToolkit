namespace Ampheck.RockchipToolkit.Rpc;

internal readonly record struct ToolkitFrame(
    byte ProtocolMajor,
    byte ProtocolMinor,
    ToolkitFrameKind Kind,
    ushort Flags,
    ToolkitOpcode Opcode,
    uint ChannelId,
    uint RequestId,
    ulong Sequence,
    ushort Status,
    byte[] Payload);
