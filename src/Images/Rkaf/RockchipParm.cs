using System.Buffers.Binary;
using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Images.Rkaf;

/// <summary>PARM wrapper used for the parameter partition inside RKAF.</summary>
internal static class RockchipParm {
    public const int Overhead = 12;
    private const uint Magic = 0x4D524150;

    // The wrapper is little-endian: "PARM" at +0, payload length at +4,
    // payload bytes at +8, and the Rockchip CRC-32 at +8+payload length.

    public static bool HasHeader(
        ReadOnlySpan<byte> prefix,
        long storedLength
    ) {
        return prefix.Length >= 8 &&
            BinaryPrimitives.ReadUInt32LittleEndian(prefix) == Magic &&
            storedLength >= Overhead &&
            BinaryPrimitives.ReadUInt32LittleEndian(prefix[4..]) ==
                storedLength - Overhead;
    }

    public static bool TryUnwrap(
        ReadOnlySpan<byte> stored,
        out ReadOnlySpan<byte> content
    ) {
        content = default;
        if (stored.Length < 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(stored) != Magic) {
            return false;
        }

        if (!HasHeader(stored, stored.Length)) {
            throw new InvalidDataException(
                "The PARM wrapper length is invalid.");
        }

        var contentLength = BinaryPrimitives.ReadUInt32LittleEndian(
            stored[4..]);
        content = stored.Slice(8, checked((int)contentLength));
        var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(
            stored[(8 + content.Length)..]);
        if (RockchipCrc32.Append(0, content) != storedCrc) {
            throw new InvalidDataException(
                "The PARM wrapper checksum is invalid.");
        }

        return true;
    }

    public static byte[] Wrap(ReadOnlySpan<byte> content) {
        var result = new byte[checked(content.Length + Overhead)];
        BinaryPrimitives.WriteUInt32LittleEndian(result, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(
            result.AsSpan(4),
            checked((uint)content.Length));
        content.CopyTo(result.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(
            result.AsSpan(8 + content.Length),
            RockchipCrc32.Append(0, content));
        return result;
    }
}
