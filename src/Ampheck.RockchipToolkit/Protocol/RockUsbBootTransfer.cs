using Ampheck.RockchipToolkit.Transport;

namespace Ampheck.RockchipToolkit.Protocol;

internal static class RockUsbBootTransfer {
    private const int ControlBlockSize = 4096;
    private const byte VendorOutRequestType = 0x40;
    private const byte VendorRequest = 0x0C;

    public static async ValueTask TransferAsync(
        IUsbTransport transport,
        ushort requestCode,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) {
        ValidateRequest(requestCode, data);

        if (transport is IMaskRomBootTransport nativeTransport) {
            await nativeTransport.DownloadBootEntryAsync(
                requestCode,
                data,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var payloadLength = data.Length;
        var sendPendingPacket =
            payloadLength % ControlBlockSize == ControlBlockSize - 2;
        if (payloadLength % ControlBlockSize == ControlBlockSize - 1) {
            payloadLength++;
        }

        var crc = CalculateCrc(data.Span, payloadLength);
        var request = new byte[checked(payloadLength + 2)];
        data.CopyTo(request);
        BinaryCodec.U16BigEndian(request, payloadLength, crc);

        await SendChunksAsync(
            transport,
            requestCode,
            request,
            cancellationToken).ConfigureAwait(false);

        if (sendPendingPacket) {
            await SendPendingPacketAsync(
                transport,
                requestCode,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendChunksAsync(
        IUsbTransport transport,
        ushort requestCode,
        byte[] request,
        CancellationToken cancellationToken) {
        for (var offset = 0; offset < request.Length; offset += ControlBlockSize) {
            var length = Math.Min(
                ControlBlockSize,
                request.Length - offset);
            var transfer = new UsbControlRequest(
                VendorOutRequestType,
                VendorRequest,
                0,
                requestCode,
                checked((uint)length));
            var transferred = await transport.ControlTransferAsync(
                transfer,
                request.AsMemory(offset, length),
                cancellationToken).ConfigureAwait(false);

            if (transferred != length) {
                throw new IOException(
                    $"MaskROM BOOT transfer 0x{requestCode:X4} " +
                    $"returned {transferred}/{length} bytes.");
            }
        }
    }

    private static async ValueTask SendPendingPacketAsync(
        IUsbTransport transport,
        ushort requestCode,
        CancellationToken cancellationToken) {
        var pending = new byte[1];
        var transferred = await transport.ControlTransferAsync(
                new UsbControlRequest(
                    VendorOutRequestType,
                    VendorRequest,
                    0,
                    requestCode,
                    1),
                pending,
                cancellationToken)
            .ConfigureAwait(false);
        if (transferred != pending.Length) {
            throw new IOException(
                $"MaskROM BOOT pending packet 0x{requestCode:X4} " +
                $"returned {transferred}/{pending.Length} bytes.");
        }
    }

    private static ushort CalculateCrc(
        ReadOnlySpan<byte> data,
        int paddedLength) {
        ushort crc = 0xFFFF;
        foreach (var value in data) {
            crc = UpdateCrc(crc, value);
        }

        for (var index = data.Length; index < paddedLength; index++) {
            crc = UpdateCrc(crc, 0);
        }

        return crc;
    }

    private static ushort UpdateCrc(ushort crc, byte value) {
        crc ^= (ushort)(value << 8);
        for (var bit = 0; bit < 8; bit++) {
            crc = (ushort)(
                (crc & 0x8000) != 0
                    ? (crc << 1) ^ 0x1021
                    : crc << 1);
        }

        return crc;
    }

    private static void ValidateRequest(
        ushort requestCode,
        ReadOnlyMemory<byte> data) {
        if (requestCode is not (0x0471 or 0x0472)) {
            throw new ArgumentOutOfRangeException(
                nameof(requestCode),
                "Rockchip BOOT entries use request 0x0471 or 0x0472.");
        }

        if (data.IsEmpty) {
            throw new ArgumentException(
                "A BOOT entry must contain data.",
                nameof(data));
        }
    }
}
