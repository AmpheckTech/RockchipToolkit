using System.Buffers.Binary;

namespace Ampheck.RockchipToolkit.Protocol;

internal static class BinaryCodec {
    public static void U16(Span<byte> target, int offset, ushort value) {
        BinaryPrimitives.WriteUInt16LittleEndian(target[offset..], value);
    }

    public static void U32(Span<byte> target, int offset, uint value) {
        BinaryPrimitives.WriteUInt32LittleEndian(target[offset..], value);
    }

    public static void U16BigEndian(Span<byte> target, int offset, ushort value) {
        BinaryPrimitives.WriteUInt16BigEndian(target[offset..], value);
    }

    public static void U32BigEndian(Span<byte> target, int offset, uint value) {
        BinaryPrimitives.WriteUInt32BigEndian(target[offset..], value);
    }

    public static uint U32BigEndian(ReadOnlySpan<byte> source, int offset) {
        return BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
    }

    public static uint U32(ReadOnlySpan<byte> source, int offset) {
        return BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
    }
}
