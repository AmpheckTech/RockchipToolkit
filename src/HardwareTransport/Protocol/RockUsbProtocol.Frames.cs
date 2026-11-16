namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

public sealed partial class RockUsbProtocol {
    private static byte[] BuildCommandBlock(
        RockUsbOperation operation,
        uint address,
        ushort length,
        ReadWriteMode mode,
        int transferLength,
        bool readData,
        byte lun,
        byte? subCode,
        uint tag
    ) {
        // CBW offsets: signature 0, tag 4, transfer length 8, flags 12,
        // LUN 13, command length 14, opcode 15, subcode 16, address 17,
        // transfer count 22. Offsets 24..30 are reserved and remain zero.
        var cbw = new byte[CbwLength];
        BinaryCodec.U32(cbw, 0, CbwSignature);
        BinaryCodec.U32(cbw, 4, tag);
        BinaryCodec.U32(cbw, 8, checked((uint)transferLength));
        cbw[12] = readData ? (byte)0x80 : (byte)0; // Direction: 1 = device-to-host.
        cbw[13] = lun;
        cbw[14] = RockUsbCommandPolicy.GetCommandBlockLength(operation);
        cbw[15] = (byte)operation;
        cbw[16] = subCode ?? (byte)mode;
        BinaryCodec.U32BigEndian(cbw, 17, address);
        BinaryCodec.U16BigEndian(cbw, 22, length);
        return cbw;
    }

    private static void ValidateCsw(
        RockUsbOperation operation,
        ReadOnlySpan<byte> csw,
        uint tag
    ) {
        // CSW offsets: signature 0, tag 4, residue 8, status 12.
        var signature = BinaryCodec.U32(csw, 0);
        var returnedTag = BinaryCodec.U32(csw, 4);
        var residue = BinaryCodec.U32(csw, 8);
        if (signature != CswSignature || returnedTag != tag) {
            throw new RockUsbException(
                "The device returned an invalid command status.",
                operation,
                residue: residue);
        }

        var status = (RockUsbStatus)csw[12];
        if (status != RockUsbStatus.Passed) {
            throw new RockUsbException(
                $"RockUSB command failed with status {status}.",
                operation,
                status: status,
                residue: residue);
        }
    }

    private async ValueTask DelayAndResetAsync(
        RockUsbOperation operation,
        Exception firstFailure,
        CancellationToken cancellationToken
    ) {
        try {
            await Task.Delay(
                TimeSpan.FromSeconds(2),
                cancellationToken).ConfigureAwait(false);
            await _transport.ResetAsync(cancellationToken)
                .ConfigureAwait(false);
        } catch (OperationCanceledException) {
            throw;
        } catch {
            throw WrapFailure(operation, firstFailure);
        }
    }

    private async ValueTask ReadExactlyAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    ) {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await _transport.ReadBulkAsync(
                buffer[offset..],
                _timeout,
                cancellationToken).ConfigureAwait(false);
            if (read <= 0) {
                throw new EndOfStreamException(
                    "The USB transport returned no data.");
            }

            offset += read;
        }
    }

    private static RockUsbException WrapFailure(
        RockUsbOperation operation,
        Exception error
    ) {
        return error as RockUsbException ??
            new RockUsbException(
                $"RockUSB {operation} failed: {error.Message}",
                operation,
                error);
    }

    private static bool IsDisconnect(Exception error) {
        for (var current = error;
            current is not null;
            current = current.InnerException) {
            if (current is System.ComponentModel.Win32Exception {
                NativeErrorCode: 22 or 31 or 1167
            } ||
                current.Message.Contains(
                    "No such device",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "Entity not found",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "device not connected",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "LIBUSB_ERROR_NO_DEVICE",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "Timeout",
                    StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
