using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.Device;

/// <summary>High-level Rockchip device facade. It owns protocol semantics, not USB enumeration.</summary>
public sealed class RockchipDevice : IAsyncDisposable {
    private const int MaxTransferSectors = 128;
    private const int MaxTransferBytes =
        MaxTransferSectors * RockUsbProtocol.SectorSize;
    private readonly IUsbTransport _transport;
    private readonly ILogger<RockchipDevice> _logger;
    private bool _disposed;

    public RockchipDevice(
        IUsbTransport transport,
        TimeSpan? commandTimeout = null,
        ILoggerFactory? loggerFactory = null) {
        _transport = transport ??
            throw new ArgumentNullException(nameof(transport));
        _logger = loggerFactory?.CreateLogger<RockchipDevice>() ??
            NullLogger<RockchipDevice>.Instance;
        Protocol = new RockUsbProtocol(
            transport,
            commandTimeout,
            loggerFactory?.CreateLogger<RockUsbProtocol>());
        _logger.LogDebug(
                "Created device facade for {VendorId:X4}:{ProductId:X4}",
            Info.VendorId,
            Info.ProductId);
    }

    public RockUsbProtocol Protocol { get; }
    public UsbDeviceInfo Info => _transport.Device;

    public async ValueTask<byte[]> ReadAsync(
        uint sector,
        ushort count,
        CancellationToken cancellationToken = default) {
        RockUsbCommandPolicy.ValidateSectorCount(count);
        RockUsbCommandPolicy.ValidateSectorRange(sector, count);
        _logger.LogDebug(
            "Reading {SectorCount} sector(s) starting at 0x{StartSector:X8}",
            count,
            sector);

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
        CancellationToken cancellationToken = default) {
        ValidateImage(sector, data);
        _logger.LogTrace(
            "Writing {ByteCount} bytes starting at sector 0x{StartSector:X8}",
            data.Length,
            sector);

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

        _logger.LogTrace("Write completed");
    }

    public async ValueTask EraseAsync(
        uint sector,
        uint count,
        CancellationToken cancellationToken = default) {
        if (count == 0) {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "The sector count must be at least one.");
        }

        RockUsbCommandPolicy.ValidateSectorRange(sector, count);
        _logger.LogInformation(
            "Erasing {SectorCount} sector(s) starting at 0x{StartSector:X8}",
            count,
            sector);
        var remaining = count;
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
        }
    }

    public ValueTask ResetAsync(
        ResetMode mode = ResetMode.Normal,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Requesting reset mode {ResetMode}", mode);
        return Protocol.ResetAsync(mode, cancellationToken);
    }

    public ValueTask TestReadyAsync(
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Checking whether the device is ready");
        return Protocol.TestReadyAsync(
            cancellationToken: cancellationToken);
    }

    public ValueTask<byte[]> GetChipInfoAsync(
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Reading chip information");
        return Protocol.ReadChipInfoAsync(cancellationToken);
    }

    public ValueTask<byte[]> GetFlashIdAsync(
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Reading flash ID");
        return Protocol.ReadFlashIdAsync(cancellationToken);
    }

    public ValueTask<byte[]> GetFlashInfoAsync(
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Reading flash information");
        return Protocol.ReadFlashInfoAsync(cancellationToken);
    }

    public ValueTask<byte[]> GetCapabilityAsync(
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Reading device capability");
        return Protocol.ReadCapabilityAsync(cancellationToken);
    }

    public ValueTask ChangeStorageAsync(
        StorageType storage,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Changing storage to {StorageType}", storage);
        return Protocol.ChangeStorageAsync(
            storage,
            cancellationToken);
    }

    /// <summary>Loads 471/472 BOOT entries into a MaskROM device.</summary>
    public ValueTask DownloadBootAsync(
        string loaderPath,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation(
            "Downloading loader from {LoaderPath}",
            loaderPath);
        return RockchipBootLoader.LoadAsync(
            Protocol,
            Info,
            _transport is IMaskRomBootTransport,
            loaderPath,
            cancellationToken,
            _logger);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _logger.LogDebug("Disposing Rockchip device");
        Protocol.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private static void ValidateImage(
        uint sector,
        ReadOnlyMemory<byte> data) {
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
