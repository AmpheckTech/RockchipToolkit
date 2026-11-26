using System.Buffers.Binary;
using System.Text;
using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Devices.Internal;

internal static class PersistentLoader {
    public const uint IdbSector = 64;

    private const int SectorSize = 512;
    private const int EntryAlign = 2048;
    private const int MaxIdbBytes = (8192 - 64) * SectorSize;

    public static async ValueTask<(
        byte[] Data,
        bool RequiresNewIdb
    )> BuildAsync(
        string path,
        CancellationToken cancellationToken
    ) {
        using var loader = LoaderImage.Open(path);
        var entries = loader.Entries
            .Where(entry => entry.RequestCode == 0)
            .ToArray();
        var flashData = FindRequired(entries, "FlashData");
        var flashBoot = FindRequired(entries, "FlashBoot");
        var flashHead = FindOptional(entries, "FlashHead");
        var data = await ReadEntryAsync(
                loader,
                flashData,
                cancellationToken)
            .ConfigureAwait(false);
        var boot = await ReadEntryAsync(
                loader,
                flashBoot,
                cancellationToken)
            .ConfigureAwait(false);
        var rc4Disabled = loader.Rc4Flag != 0;
        if (rc4Disabled) {
            DecodeSectors(data);
            DecodeSectors(boot);
        }

        if (flashHead is not null) {
            var head = await ReadEntryAsync(
                    loader,
                    flashHead,
                    cancellationToken)
                .ConfigureAwait(false);
            var plainHead = head.ToArray();
            if (rc4Disabled) {
                DecodeSectors(head);
                plainHead = head;
            } else {
                DecodeSectors(plainHead);
            }

            ValidateNewHead(plainHead);
            var headLength = Align(head.Length);
            var dataLength = Align(data.Length);
            var bootLength = Align(boot.Length);
            var total = CheckedIdbLength(
                headLength + dataLength + bootLength);
            var idb = new byte[total];
            head.CopyTo(idb, 0);
            data.CopyTo(idb, headLength);
            boot.CopyTo(idb, headLength + dataLength);
            return (idb, true);
        }

        return (BuildLegacy(data, boot, rc4Disabled), false);
    }

    private static LoaderImageEntry FindRequired(
        IReadOnlyList<LoaderImageEntry> entries,
        string name
    ) {
        return FindOptional(entries, name) ??
            throw new InvalidDataException(
                $"MiniLoader has no {name} persistent entry.");
    }

    private static LoaderImageEntry? FindOptional(
        IReadOnlyList<LoaderImageEntry> entries,
        string name
    ) {
        var matches = entries
            .Where(entry => entry.Name.Equals(
                name,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidDataException(
                $"MiniLoader has duplicate {name} persistent entries.")
        };
    }

    private static async Task<byte[]> ReadEntryAsync(
        LoaderImage loader,
        LoaderImageEntry entry,
        CancellationToken cancellationToken
    ) {
        if (entry.Size == 0 || entry.Size % SectorSize != 0) {
            throw new InvalidDataException(
                $"{entry.Name} must be nonempty and 512-byte aligned.");
        }

        if (entry.Size > int.MaxValue) {
            throw new InvalidDataException(
                $"{entry.Name} is too large to load.");
        }

        var data = new byte[(int)entry.Size];
        await using var stream = loader.OpenBootRange(
            entry.Offset,
            entry.Size);
        await stream.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        return data;
    }

    private static byte[] BuildLegacy(
        byte[] data,
        byte[] boot,
        bool rc4Disabled
    ) {
        var dataLength = Align(data.Length);
        var bootLength = Align(boot.Length);
        var total = CheckedIdbLength(EntryAlign + dataLength + bootLength);
        var dataSectors = checked((ushort)(dataLength / SectorSize));
        var codeSectors = checked((ushort)((dataLength + bootLength) / SectorSize));
        var idb = new byte[total];
        WriteUInt32(idb, 0, 0x0FF0AA55);
        WriteUInt32(idb, 8, rc4Disabled ? 1u : 0u);
        WriteUInt16(idb, 12, 4);
        WriteUInt16(idb, 14, 4);
        WriteUInt16(idb, 506, dataSectors);
        WriteUInt16(idb, 508, codeSectors);
        WriteUInt16(idb, SectorSize, 0x0C);
        WriteUInt16(idb, SectorSize + 2, 0xFFFF);
        WriteUInt32(idb, SectorSize + 10, 0x38324B52);
        data.CopyTo(idb, EntryAlign);
        boot.CopyTo(idb, EntryAlign + dataLength);

        var sector0Crc = Crc16(idb.AsSpan(0, SectorSize));
        var sector1Crc = Crc16(idb.AsSpan(SectorSize, SectorSize));
        var sector3Crc = Crc16(idb.AsSpan(
            SectorSize * 3,
            SectorSize));
        var codeCrc = RockchipCrc32.Append(
            0,
            idb.AsSpan(EntryAlign));
        const int sector2 = SectorSize * 2;
        Encoding.ASCII.GetBytes("VC").CopyTo(idb, sector2 + 491);
        WriteUInt16(idb, sector2 + 494, sector0Crc);
        WriteUInt16(idb, sector2 + 496, sector1Crc);
        WriteUInt32(idb, sector2 + 498, codeCrc);
        Encoding.ASCII.GetBytes("CRC").CopyTo(idb, sector2 + 506);
        WriteUInt16(idb, sector2 + 510, sector3Crc);
        foreach (var sector in new[] { 0, 2, 3 }) {
            Rc4(idb.AsSpan(
                sector * SectorSize,
                SectorSize));
        }

        return idb;
    }

    private static void ValidateNewHead(ReadOnlySpan<byte> head) {
        if (head.Length < EntryAlign ||
            !head[..4].SequenceEqual("RKNS"u8)) {
            throw new InvalidDataException(
                "FlashHead must be an unsigned RKNS IDB header.");
        }

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(
            head[12..16]);
        if (flags > 2) {
            throw new InvalidDataException(
                "Signed, secured, or unknown RKNS IDB flags are not supported.");
        }
    }

    private static int Align(int value) {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return checked(((value + EntryAlign - 1) / EntryAlign) * EntryAlign);
    }

    private static int CheckedIdbLength(int value) {
        if (value <= 0 || value > MaxIdbBytes) {
            throw new InvalidDataException(
                "Persistent loader exceeds the supported IDB area.");
        }

        return value;
    }

    private static void DecodeSectors(Span<byte> data) {
        for (var offset = 0; offset < data.Length; offset += SectorSize) {
            Rc4(data.Slice(offset, SectorSize));
        }
    }

    private static void Rc4(Span<byte> data) {
        ReadOnlySpan<byte> key = [
            124, 78, 3, 4, 85, 5, 9, 7,
            45, 44, 123, 56, 23, 13, 23, 17
        ];
        Span<byte> state = stackalloc byte[256];
        for (var index = 0; index < state.Length; index++) {
            state[index] = (byte)index;
        }

        var j = 0;
        for (var index = 0; index < state.Length; index++) {
            j = (j + state[index] + key[index % key.Length]) & 0xFF;
            (state[index], state[j]) = (state[j], state[index]);
        }

        var i = 0;
        j = 0;
        for (var index = 0; index < data.Length; index++) {
            i = (i + 1) & 0xFF;
            j = (j + state[i]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
            data[index] ^= state[(state[i] + state[j]) & 0xFF];
        }
    }

    private static ushort Crc16(ReadOnlySpan<byte> data) {
        ushort crc = 0;
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

    private static void WriteUInt16(
        Span<byte> destination,
        int offset,
        ushort value
    ) => BinaryPrimitives.WriteUInt16LittleEndian(
        destination[offset..],
        value);

    private static void WriteUInt32(
        Span<byte> destination,
        int offset,
        uint value
    ) => BinaryPrimitives.WriteUInt32LittleEndian(
        destination[offset..],
        value);
}
