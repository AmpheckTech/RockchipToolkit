namespace Ampheck.RockchipToolkit.Rpc;

internal enum MdnsRecordType : ushort {
    A = 1,
    Ptr = 12,
    Txt = 16,
    Srv = 33,
    Any = 255
}
