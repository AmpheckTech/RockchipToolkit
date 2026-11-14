using System.Buffers.Binary;

namespace Ampheck.RockchipToolkit.Protocol;

public static class RockchipCapabilityParser {
    private const int CapabilityLength = 8;

    public static RockchipCapabilityInfo Parse(ReadOnlySpan<byte> data) {
        if (data.Length < CapabilityLength) {
            throw new InvalidDataException(
                "The capability response is shorter than eight bytes.");
        }

        // The capability response is an 8-byte little-endian feature bitfield.
        var raw = BinaryPrimitives.ReadUInt64LittleEndian(data);
        return new RockchipCapabilityInfo(
            (RockchipCapabilityFlags)raw,
            Convert.ToHexString(data[..CapabilityLength]));
    }
}
