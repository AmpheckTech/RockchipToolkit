using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

public sealed partial class RockUsbProtocol {
    private async ValueTask<int> ExecuteAsync(
        RockUsbOperation operation,
        uint address,
        ushort length,
        ReadWriteMode mode,
        ReadOnlyMemory<byte> payload,
        bool readData,
        CancellationToken cancellationToken,
        Memory<byte> readBuffer = default,
        byte lun = 0,
        byte? subCode = null,
        int? readMaximumLength = null
    ) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            return await ExecuteWithRetryAsync(
                operation,
                address,
                length,
                mode,
                payload,
                readData,
                cancellationToken,
                readBuffer,
                lun,
                subCode,
                readMaximumLength).ConfigureAwait(false);
        } finally {
            _commandGate.Release();
        }
    }

    private async ValueTask<int> ExecuteWithRetryAsync(
        RockUsbOperation operation,
        uint address,
        ushort length,
        ReadWriteMode mode,
        ReadOnlyMemory<byte> payload,
        bool readData,
        CancellationToken cancellationToken,
        Memory<byte> readBuffer,
        byte lun,
        byte? subCode,
        int? readMaximumLength
    ) {
        Exception? firstFailure = null;
        var tag = unchecked(++_tag);
        var attempts = RockUsbCommandPolicy.CanRetry(operation) ? 2 : 1;
        _logger.LogTrace(
            "Executing {Operation}: address=0x{Address:X8}, " +
            "length={Length}, tag=0x{Tag:X8}, attempts={Attempts}",
            operation,
            address,
            length,
            tag,
            attempts);

        for (var attempt = 0; attempt < attempts; attempt++) {
            try {
                var actualLength = await ExecuteOnceAsync(
                        operation,
                        address,
                        length,
                        mode,
                        payload,
                        readData,
                        cancellationToken,
                        readBuffer,
                        lun,
                        subCode,
                        readMaximumLength,
                        tag)
                    .ConfigureAwait(false);
                _logger.LogTrace(
                    "Completed {Operation} with {ActualLength} byte(s)",
                    operation,
                    actualLength);
                return actualLength;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception error) when (
                attempt == 0 &&
                RockUsbCommandPolicy.CanRetry(operation)) {
                firstFailure = error;
                _logger.LogWarning(
                    error,
                    "{Operation} failed; resetting the device and retrying",
                    operation);
                await DelayAndResetAsync(
                    operation,
                    firstFailure,
                    cancellationToken).ConfigureAwait(false);
            } catch (Exception error) {
                throw WrapFailure(operation, error);
            }
        }

        throw WrapFailure(
            operation,
            firstFailure ??
                new IOException("The RockUSB operation failed."));
    }

    private async ValueTask<int> ExecuteOnceAsync(
        RockUsbOperation operation,
        uint address,
        ushort length,
        ReadWriteMode mode,
        ReadOnlyMemory<byte> payload,
        bool readData,
        CancellationToken cancellationToken,
        Memory<byte> readBuffer,
        byte lun,
        byte? subCode,
        int? readMaximumLength,
        uint tag
    ) {
        var cbw = BuildCommandBlock(
            operation,
            address,
            length,
            mode,
            payload.Length,
            readData,
            lun,
            subCode,
            tag);

        if (!readData &&
            !payload.IsEmpty &&
            _transport is IUsbBulkSequenceTransport sequenceTransport) {
            await sequenceTransport.WriteBulkSequenceAsync(
                    cbw,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);
        } else {
            await _transport.WriteBulkAsync(cbw, cancellationToken)
                .ConfigureAwait(false);
            if (!readData && !payload.IsEmpty) {
                await _transport.WriteBulkAsync(payload, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var actualReadLength = readData
            ? await ReadPayloadAsync(
                readBuffer,
                readMaximumLength,
                cancellationToken).ConfigureAwait(false)
            : 0;

        var csw = new byte[CswLength];
        await ReadExactlyAsync(csw, cancellationToken)
            .ConfigureAwait(false);
        ValidateCsw(operation, csw, tag);
        _logger.LogTrace(
            "Completed command block for {Operation} with status {Status} " +
            "and residue {Residue}",
            operation,
            (RockUsbStatus)csw[12],
            BinaryCodec.U32(csw, 8));
        return actualReadLength;
    }

    private async ValueTask<int> ReadPayloadAsync(
        Memory<byte> readBuffer,
        int? readMaximumLength,
        CancellationToken cancellationToken
    ) {
        if (readMaximumLength is not { } maximumLength) {
            await ReadExactlyAsync(readBuffer, cancellationToken)
                .ConfigureAwait(false);
            return readBuffer.Length;
        }

        var actualLength = await _transport.ReadBulkAsync(
            readBuffer[..maximumLength],
            _timeout,
            cancellationToken).ConfigureAwait(false);
        if (actualLength <= 0) {
            throw new EndOfStreamException(
                "The USB transport returned no data.");
        }

        if (actualLength > maximumLength) {
            throw new IOException(
                "The USB transport returned more data than the buffer can hold.");
        }

        return actualLength;
    }
}
