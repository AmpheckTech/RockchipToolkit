namespace Ampheck.RockchipToolkit.Images;

/// <summary>
/// Rockchip's non-reflected CRC-32 (polynomial 0x04C10DB7, initial value 0).
/// It is used by the RKAF trailer and by PARM-wrapped parameter files.
/// </summary>
internal static class RockchipCrc32 {
    private const int BufferSize = 4 * 1024 * 1024;
    private const uint Polynomial = 0x04C10DB7;
    private static readonly uint[] Table = CreateTable();

    public static uint Append(uint crc, ReadOnlySpan<byte> data) {
        foreach (var value in data) {
            var index = ((crc >> 24) ^ value) & 0xff;
            crc = (crc << 8) ^ Table[index];
        }

        return crc;
    }

    public static async Task<uint> ComputeAsync(
        Stream stream,
        long offset,
        long length,
        Action<long>? advance,
        CancellationToken cancellationToken) {
        StreamRange.Validate(
            stream.Length,
            offset,
            length,
            "CRC input");
        stream.Position = offset;
        var buffer = new byte[BufferSize];
        var crc = 0u;
        var remaining = length;
        while (remaining > 0) {
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException(
                    "The Rockchip image ended unexpectedly.");
            }

            crc = Append(crc, buffer.AsSpan(0, read));
            advance?.Invoke(read);
            remaining -= read;
        }

        return crc;
    }

    private static uint[] CreateTable() {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++) {
            var crc = (uint)index << 24;
            for (var bit = 0; bit < 8; bit++) {
                crc = (crc & 0x80000000) != 0
                    ? (crc << 1) ^ Polynomial
                    : crc << 1;
            }

            table[index] = crc;
        }

        return table;
    }
}
