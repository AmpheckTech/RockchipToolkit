namespace Ampheck.RockchipToolkit.Rpc;

internal readonly record struct MdnsQuestion(
    string Name,
    MdnsRecordType Type,
    ushort Class);
