using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

internal sealed class ToolkitTlvReader {
    private readonly Dictionary<ToolkitWireTag, List<byte[]>> _values = [];

    public ToolkitTlvReader(ReadOnlySpan<byte> payload) {
        var offset = 0;
        while (offset < payload.Length) {
            if (payload.Length - offset < 6) {
                throw new InvalidDataException("The toolkit TLV header is incomplete.");
            }

            var tag = (ToolkitWireTag)BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(payload[(offset + 2)..]);
            offset += 6;
            if (length > int.MaxValue || offset > payload.Length - (int)length) {
                throw new InvalidDataException("The toolkit TLV value is invalid.");
            }

            if (!_values.TryGetValue(tag, out var values)) {
                _values[tag] = values = [];
            }

            values.Add(payload.Slice(offset, (int)length).ToArray());
            offset += (int)length;
        }
    }

    public string? GetString(ToolkitWireTag tag) =>
        GetFirst(tag) is { } value ? Encoding.UTF8.GetString(value) : null;

    public ushort? GetUInt16(ToolkitWireTag tag) =>
        GetFirst(tag) is { Length: 2 } value ? BinaryPrimitives.ReadUInt16LittleEndian(value) : null;

    public uint? GetUInt32(ToolkitWireTag tag) =>
        GetFirst(tag) is { Length: 4 } value ? BinaryPrimitives.ReadUInt32LittleEndian(value) : null;

    public ulong? GetUInt64(ToolkitWireTag tag) =>
        GetFirst(tag) is { Length: 8 } value ? BinaryPrimitives.ReadUInt64LittleEndian(value) : null;

    public byte? GetByte(ToolkitWireTag tag) =>
        GetFirst(tag) is { Length: 1 } value ? value[0] : null;

    public bool? GetBoolean(ToolkitWireTag tag) =>
        GetByte(tag) is { } value ? value != 0 : null;

    public IReadOnlyList<byte[]> GetValues(ToolkitWireTag tag) =>
        _values.TryGetValue(tag, out var values) ? values : [];

    private byte[]? GetFirst(ToolkitWireTag tag) =>
        _values.TryGetValue(tag, out var values) && values.Count > 0 ? values[0] : null;
}
