using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

public sealed partial class RockUsbProtocol {
    public async ValueTask ResetAsync(
        ResetMode mode = ResetMode.Normal,
        CancellationToken cancellationToken = default
    ) {
        if (!Enum.IsDefined(mode)) {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        await _commandGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            var tag = unchecked(++_tag);
            var cbw = BuildCommandBlock(
                RockUsbOperation.DeviceReset,
                0,
                0,
                ReadWriteMode.Image,
                0,
                false,
                0,
                (byte)mode,
                tag);
            try {
                await _transport.WriteBulkAsync(
                        cbw,
                        cancellationToken)
                    .ConfigureAwait(false);
                await Task.Delay(
                        TimeSpan.FromMilliseconds(250),
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch (Exception error) when (
                IsDisconnect(error)) {
                _logger.LogDebug(
                    error,
                    "Device disconnected while requesting reset");
            }
        } finally {
            _commandGate.Release();
        }

        try {
            await _transport.ResetAsync(cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            IsDisconnect(error)) {
            _logger.LogDebug(
                error,
                "USB device disconnected during reset");
        }
    }

    public async ValueTask ChangeStorageAsync(
        StorageType storage,
        CancellationToken cancellationToken = default
    ) {
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
        CancellationToken cancellationToken = default
    ) {
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
}
