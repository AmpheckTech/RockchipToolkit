namespace Ampheck.RockchipToolkit.Rpc;

internal sealed class MdnsPacket {
    public List<MdnsQuestion> Questions { get; } = [];
    public List<MdnsResourceRecord> Answers { get; } = [];
    public List<MdnsResourceRecord> AdditionalRecords { get; } = [];
}
