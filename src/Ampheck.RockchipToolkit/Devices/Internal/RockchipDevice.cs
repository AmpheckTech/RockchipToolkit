using Ampheck.RockchipToolkit.Protocol.RockUsb;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Devices.Internal;

/// <summary>High-level Rockchip device facade. It owns protocol semantics, not USB enumeration.</summary>
internal sealed class RockchipDevice {
    private const int MaxTransferSectors = 128;
    private const int MaxTransferBytes =
        MaxTransferSectors * RockUsbProtocol.SectorSize;
    private const int EraseProgressReports = 100;
    private readonly IUsbTransport _transport;
    private readonly RockchipBootLoader _loaderDownloader;
    private bool _disposed;

    public RockchipDevice(
        IUsbTransport transport,
        RockchipBootLoader loaderDownloader,
        TimeSpan? commandTimeout = null
    ) {
        _transport = transport ??
            throw new ArgumentNullException(nameof(transport));
        _loaderDownloader = loaderDownloader ??
            throw new ArgumentNullException(nameof(loaderDownloader));
        Protocol = new RockUsbProtocol(transport, commandTimeout);
    }

    public RockUsbProtocol Protocol { get; }
    public UsbDeviceInfo Info => _transport.Device;

    public async ValueTask<byte[]> ReadAsync(
        uint sector,
        ushort count,
        CancellationToken cancellationToken = default
    ) {
        RockUsbCommandPolicy.ValidateSectorCount(count);
        RockUsbCommandPolicy.ValidateSectorRange(sector, count);
        var result = new byte[count * RockUsbProtocol.SectorSize];
        var offset = 0;
        while (offset < result.Length) {
            var remainingSectors =
                (count - offset / RockUsbProtocol.SectorSize) switch {
                    > MaxTransferSectors => (ushort)MaxTransferSectors,
                    var value => (ushort)value
                };
            var chunk = await Protocol.ReadLbaAsync(
                    sector,
                    remainingSectors,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            chunk.CopyTo(result, offset);
            offset += chunk.Length;
            sector = checked(sector + remainingSectors);
        }

        return result;
    }

    /// <summary>Writes an aligned image, splitting it into protocol-sized transfers when necessary.</summary>
    public async ValueTask WriteAsync(
        uint sector,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        ValidateImage(sector, data);
        var offset = 0;
        while (offset < data.Length) {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(
                MaxTransferBytes,
                data.Length - offset);
            await Protocol.WriteLbaAsync(
                sector,
                data.Slice(offset, length),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            offset += length;
            if (offset < data.Length) {
                sector = checked(
                    sector +
                    (uint)(length / RockUsbProtocol.SectorSize));
            }
        }
    }

    public ValueTask EraseAsync(
        uint sector,
        uint count,
        CancellationToken cancellationToken = default
    ) =>
        EraseAsync(
                sector,
                count,
                progress: null,
                cancellationToken);

    public async ValueTask EraseAsync(
        uint sector,
        uint count,
        Func<long, long, ValueTask>? progress,
        CancellationToken cancellationToken = default
    ) {
        if (count == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "The sector count must be at least one.");
        }

        RockUsbCommandPolicy.ValidateSectorRange(sector, count);
        var remaining = count;
        var erased = 0u;
        var reportInterval = Math.Max(
            1u,
            count / EraseProgressReports);
        var nextReport = reportInterval;
        var totalBytes = checked(
            (long)count * RockUsbProtocol.SectorSize);
        while (remaining > 0) {
            var chunk = (ushort)Math.Min(
                remaining,
                MaxTransferSectors);
            await Protocol.EraseLbaAsync(
                    sector,
                    chunk,
                    cancellationToken)
                .ConfigureAwait(false);
            sector = checked(sector + chunk);
            remaining -= chunk;
            erased += chunk;
            if (progress is not null &&
                (remaining == 0 || erased >= nextReport)) {
                await progress(
                        checked(
                            (long)erased *
                            RockUsbProtocol.SectorSize),
                        totalBytes)
                    .ConfigureAwait(false);
                if (remaining > 0) {
                    nextReport = checked(
                        erased + reportInterval);
                }
            }
        }
    }

    public ValueTask ResetAsync(
        ResetMode mode = ResetMode.Normal,
        CancellationToken cancellationToken = default
    ) => Protocol.ResetAsync(mode, cancellationToken);

    public ValueTask<byte[]> GetChipInfoAsync(
        CancellationToken cancellationToken = default
    ) => Protocol.ReadChipInfoAsync(cancellationToken);

    public ValueTask<byte[]> GetFlashIdAsync(
        CancellationToken cancellationToken = default
    ) => Protocol.ReadFlashIdAsync(cancellationToken);

    public ValueTask<byte[]> GetFlashInfoAsync(
        CancellationToken cancellationToken = default
    ) => Protocol.ReadFlashInfoAsync(cancellationToken);

    public ValueTask<byte[]> GetCapabilityAsync(
        CancellationToken cancellationToken = default
    ) => Protocol.ReadCapabilityAsync(cancellationToken);

    public ValueTask ChangeStorageAsync(
        StorageType storage,
        CancellationToken cancellationToken = default
    ) => Protocol.ChangeStorageAsync(storage, cancellationToken);

    /// <summary>Loads a Rockchip boot image into a MaskROM device.</summary>
    public ValueTask DownloadBootAsync(
        string loaderPath,
        CancellationToken cancellationToken = default
    ) => _loaderDownloader.DownloadAsync(
        Protocol,
        Info,
        _transport is IMaskRomBootTransport,
        loaderPath,
        cancellationToken);

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Protocol.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private static void ValidateImage(
        uint sector,
        ReadOnlyMemory<byte> data
    ) {
        if (data.Length == 0 ||
            data.Length % RockUsbProtocol.SectorSize != 0) {
            throw new ArgumentException(
                "The data length must contain at least one complete 512-byte sector.",
                nameof(data));
        }

        var totalSectors =
            (ulong)data.Length / RockUsbProtocol.SectorSize;
        if (totalSectors >
            (ulong)uint.MaxValue + 1 - sector) {
            throw new ArgumentOutOfRangeException(
                nameof(sector),
                "The image exceeds the 32-bit RockUSB address space.");
        }
    }
}
