namespace Ampheck.RockchipToolkit.RockUsb.Transport;

internal static class Crc16Ccitt {
    public static ushort Compute(ReadOnlySpan<byte> data) {
        ushort crc = 0xFFFF;
        foreach (var value in data) {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++) {
                crc = (ushort)(
                    (crc & 0x8000) != 0
                        ? (crc << 1) ^ 0x1021
                        : crc << 1);
            }
        }

        return crc;
    }
}
