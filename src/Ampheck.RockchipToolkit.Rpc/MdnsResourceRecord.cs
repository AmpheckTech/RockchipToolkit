using System.Net;

namespace Ampheck.RockchipToolkit.Rpc;

internal sealed record MdnsResourceRecord(
    string Name,
    MdnsRecordType Type,
    ushort Class,
    uint Ttl,
    string? TargetName = null,
    ushort Port = 0,
    IReadOnlyList<byte[]>? Text = null,
    IPAddress? Address = null);
