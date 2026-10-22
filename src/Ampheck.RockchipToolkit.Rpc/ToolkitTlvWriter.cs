using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

internal sealed class ToolkitTlvWriter {
    private readonly MemoryStream _stream = new();

    public void Write(ToolkitWireTag tag, ReadOnlySpan<byte> value) {
        Span<byte> header = stackalloc byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)tag);
        BinaryPrimitives.WriteUInt32LittleEndian(header[2..], checked((uint)value.Length));
        _stream.Write(header);
        _stream.Write(value);
    }

    public void WriteString(ToolkitWireTag tag, string value) => Write(tag, Encoding.UTF8.GetBytes(value));

    public void WriteBoolean(ToolkitWireTag tag, bool value) => Write(tag, [(byte)(value ? 1 : 0)]);

    public void WriteByte(ToolkitWireTag tag, byte value) => Write(tag, [value]);

    public void WriteUInt16(ToolkitWireTag tag, ushort value) {
        Span<byte> data = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        Write(tag, data);
    }

    public void WriteUInt32(ToolkitWireTag tag, uint value) {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        Write(tag, data);
    }

    public void WriteUInt64(ToolkitWireTag tag, ulong value) {
        Span<byte> data = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(data, value);
        Write(tag, data);
    }

    public byte[] ToArray() => _stream.ToArray();
}
