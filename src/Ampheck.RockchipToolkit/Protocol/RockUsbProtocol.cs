using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.Protocol;

/// <summary>Implements the RockUSB UFI-like bulk protocol without owning a USB backend.</summary>
public sealed class RockUsbProtocol : IDisposable {
    public const int SectorSize = 512;
    public const ushort MaxSectorsPerTransfer = ushort.MaxValue;
    public const ushort MaxEraseBlocksPerTransfer = 16;

    private const int CbwLength = 31;
    private const int CswLength = 13;
    private const int MaxFlashInfoLength = 512;
    private const uint CbwSignature = 0x43425355;
    private const uint CswSignature = 0x53425355;
    private readonly IUsbTransport _transport;
    private readonly TimeSpan _timeout;
    private readonly ILogger<RockUsbProtocol> _logger;
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private uint _tag = 0xA5A50000;
    private bool _disposed;

    public RockUsbProtocol(
        IUsbTransport transport,
        TimeSpan? timeout = null,
        ILogger<RockUsbProtocol>? logger = null) {
        _transport = transport ??
            throw new ArgumentNullException(nameof(transport));
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _logger = logger ?? NullLogger<RockUsbProtocol>.Instance;
        if (_timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The command timeout must be positive.");
        }
    }

    public async ValueTask<byte[]> ReadLbaAsync(
        uint startSector,
        ushort sectorCount,
        ReadWriteMode mode = ReadWriteMode.Image,
        CancellationToken cancellationToken = default) {
        RockUsbCommandPolicy.ValidateSectorCount(sectorCount);
        RockUsbCommandPolicy.ValidateSectorRange(
            startSector,
            sectorCount);

        var data = new byte[checked(sectorCount * SectorSize)];
        await ExecuteAsync(
            RockUsbOperation.ReadLba,
            startSector,
            sectorCount,
            mode,
            data,
            true,
            cancellationToken,
            data).ConfigureAwait(false);
        return data;
    }

    public async ValueTask WriteLbaAsync(
        uint startSector,
        ReadOnlyMemory<byte> data,
        ReadWriteMode mode = ReadWriteMode.Image,
        CancellationToken cancellationToken = default) {
        if (data.Length == 0 ||
            data.Length % SectorSize != 0 ||
            data.Length / SectorSize > MaxSectorsPerTransfer) {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Data must contain 1..{MaxSectorsPerTransfer} complete sectors.");
        }

        var sectorCount =
            checked((ushort)(data.Length / SectorSize));
        RockUsbCommandPolicy.ValidateSectorRange(
            startSector,
            sectorCount);
        await ExecuteAsync(
            RockUsbOperation.WriteLba,
            startSector,
            sectorCount,
            mode,
            data,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask EraseLbaAsync(
        uint startSector,
        ushort sectorCount,
        CancellationToken cancellationToken = default) {
        RockUsbCommandPolicy.ValidateSectorCount(sectorCount);
        RockUsbCommandPolicy.ValidateSectorRange(
            startSector,
            sectorCount);
        await ExecuteAsync(
            RockUsbOperation.EraseLba,
            startSector,
            sectorCount,
            ReadWriteMode.Image,
            ReadOnlyMemory<byte>.Empty,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask EraseBlocksAsync(
        byte flashChipSelect,
        uint block,
        ushort count,
        EraseMode mode = EraseMode.Normal,
        CancellationToken cancellationToken = default) {
        if (count is 0 or > MaxEraseBlocksPerTransfer) {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"The block count must be between 1 and {MaxEraseBlocksPerTransfer}.");
        }

        if (mode is not (EraseMode.Normal or EraseMode.Force)) {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        await ExecuteAsync(
            (RockUsbOperation)mode,
            block,
            count,
            ReadWriteMode.Image,
            ReadOnlyMemory<byte>.Empty,
            false,
            cancellationToken,
            lun: flashChipSelect).ConfigureAwait(false);
    }

    public async ValueTask ResetAsync(
        ResetMode mode = ResetMode.Normal,
        CancellationToken cancellationToken = default) {
        if (!Enum.IsDefined(mode)) {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        await ExecuteAsync(
            RockUsbOperation.DeviceReset,
            0,
            0,
            ReadWriteMode.Image,
            ReadOnlyMemory<byte>.Empty,
            false,
            cancellationToken,
            subCode: (byte)mode).ConfigureAwait(false);
    }

    public ValueTask TestReadyAsync(
        byte subCode = 0,
        CancellationToken cancellationToken = default) {
        return ExecuteWithoutResultAsync(
            RockUsbOperation.TestUnitReady,
            cancellationToken,
            subCode);
    }

    public async ValueTask ChangeStorageAsync(
        StorageType storage,
        CancellationToken cancellationToken = default) {
        if (!Enum.IsDefined(storage)) {
            throw new ArgumentOutOfRangeException(nameof(storage));
        }

        await ExecuteAsync(
            RockUsbOperation.ChangeStorage,
            0,
            0,
            ReadWriteMode.Image,
            ReadOnlyMemory<byte>.Empty,
            false,
            cancellationToken,
            subCode: (byte)storage).ConfigureAwait(false);
    }

    /// <summary>Downloads one BOOT entry through the MaskROM path used by rkdeveloptool.</summary>
    public async ValueTask DownloadBootEntryAsync(
        ushort requestCode,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _logger.LogTrace(
            "Downloading BOOT entry request 0x{RequestCode:X4} " +
            "with {ByteCount} bytes",
            requestCode,
            data.Length);
        await _commandGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await RockUsbBootTransfer.TransferAsync(
                _transport,
                requestCode,
                data,
                cancellationToken).ConfigureAwait(false);
        } finally {
            _commandGate.Release();
        }
    }

    public ValueTask<byte[]> ReadChipInfoAsync(
        CancellationToken cancellationToken = default) {
        return ReadFixedAsync(
            RockUsbOperation.ReadChipInfo,
            16,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadFlashIdAsync(
        CancellationToken cancellationToken = default) {
        return ReadFixedAsync(
            RockUsbOperation.ReadFlashId,
            5,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadFlashInfoAsync(
        CancellationToken cancellationToken = default) {
        return ReadVariableAsync(
            RockUsbOperation.ReadFlashInfo,
            11,
            MaxFlashInfoLength,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadCapabilityAsync(
        CancellationToken cancellationToken = default) {
        return ReadFixedAsync(
            RockUsbOperation.ReadCapability,
            8,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadStorageMaskAsync(
        CancellationToken cancellationToken = default) {
        return ReadFixedAsync(
            RockUsbOperation.ReadStorage,
            4,
            cancellationToken);
    }

    private async ValueTask<byte[]> ReadFixedAsync(
        RockUsbOperation operation,
        int length,
        CancellationToken cancellationToken) {
        var data = new byte[length];
        await ExecuteAsync(
            operation,
            0,
            0,
            ReadWriteMode.Image,
            data,
            true,
            cancellationToken,
            data).ConfigureAwait(false);
        return data;
    }

    private async ValueTask<byte[]> ReadVariableAsync(
        RockUsbOperation operation,
        int declaredLength,
        int maximumLength,
        CancellationToken cancellationToken) {
        var data = new byte[maximumLength];
        var actualLength = await ExecuteAsync(
            operation,
            0,
            0,
            ReadWriteMode.Image,
            data.AsMemory(0, declaredLength),
            true,
            cancellationToken,
            data,
            readMaximumLength: maximumLength).ConfigureAwait(false);

        if (actualLength < declaredLength) {
            throw new RockUsbException(
                $"The device returned only {actualLength} bytes for " +
                $"{operation}; at least {declaredLength} were required.",
                operation);
        }

        return data.AsSpan(0, actualLength).ToArray();
    }

    private async ValueTask ExecuteWithoutResultAsync(
        RockUsbOperation operation,
        CancellationToken cancellationToken,
        byte subCode) {
        await ExecuteAsync(
            operation,
            0,
            0,
            ReadWriteMode.Image,
            ReadOnlyMemory<byte>.Empty,
            false,
            cancellationToken,
            subCode: subCode).ConfigureAwait(false);
    }

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
        int? readMaximumLength = null) {
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
        int? readMaximumLength) {
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
        uint tag) {
        var cbw = BuildCommandBlock(
            operation,
            address,
            length,
            mode,
            payload.Length,
            payload,
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
                operation,
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
        RockUsbOperation operation,
        Memory<byte> readBuffer,
        int? readMaximumLength,
        CancellationToken cancellationToken) {
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

    private static byte[] BuildCommandBlock(
        RockUsbOperation operation,
        uint address,
        ushort length,
        ReadWriteMode mode,
        int transferLength,
        ReadOnlyMemory<byte> payload,
        bool readData,
        byte lun,
        byte? subCode,
        uint tag) {
        var cbw = new byte[CbwLength];
        BinaryCodec.U32(cbw, 0, CbwSignature);
        BinaryCodec.U32(cbw, 4, tag);
        BinaryCodec.U32(cbw, 8, checked((uint)transferLength));
        cbw[12] = readData ? (byte)0x80 : (byte)0;
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
        uint tag) {
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
        CancellationToken cancellationToken) {
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
        CancellationToken cancellationToken) {
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
        Exception error) {
        return error as RockUsbException ??
            new RockUsbException(
                $"RockUSB {operation} failed: {error.Message}",
                operation,
                error);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _logger.LogTrace("Disposing RockUSB protocol");
        _commandGate.Dispose();
    }
}
