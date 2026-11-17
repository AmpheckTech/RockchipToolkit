using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Devices.Internal;

internal sealed class DeviceWriter {
    private readonly RockchipDevice _device;
    private readonly Func<long, long, ValueTask>? _progress;
    private readonly Func<long, long, ValueTask>? _eraseProgress;

    public DeviceWriter(
        RockchipDevice device,
        Func<long, long, ValueTask>? progress = null,
        Func<long, long, ValueTask>? eraseProgress = null
    ) {
        _device = device;
        _progress = progress;
        _eraseProgress = eraseProgress;
    }

    private const int IoBufferSize = 1 << 20;

    public async Task WriteAsync(
        string path,
        DeviceWriteOptions options,
        CancellationToken cancellationToken = default
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBackupDoesNotOverwriteSource(
            path,
            options.BackupPath);
        await using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            IoBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await WriteAsync(
                input,
                options,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task WriteAsync(
        Stream input,
        DeviceWriteOptions options,
        CancellationToken cancellationToken = default
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!input.CanRead || !input.CanSeek) {
            throw new ArgumentException(
                "The input stream must be readable and seekable.",
                nameof(input));
        }

        var offset = options.SourceOffset ?? 0;
        if (offset < 0 || offset >= input.Length &&
            input.Length != 0) {
            throw new InvalidDataException(
                "The source offset is outside the input file.");
        }

        var remainingLength = input.Length - offset;
        var writeLength = options.Length ?? remainingLength;
        if (writeLength < 0 || writeLength > remainingLength) {
            throw new InvalidDataException(
                "The requested write length exceeds the input file.");
        }

        input.Position = offset;
        ValidateLength(writeLength, options.Sector);
        if (options.BackupPath is not null) {
            await BackupAsync(
                    options.Sector,
                    writeLength,
                    options.BackupPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (options.EraseBeforeWrite) {
            var sectorCount = checked(
                (uint)(writeLength /
                    RockUsbProtocol.SectorSize));
            await _device.EraseAsync(
                    options.Sector,
                    sectorCount,
                    _eraseProgress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var buffer = new byte[IoBufferSize];
        var sector = options.Sector;
        long totalRead = 0;
        while (totalRead < writeLength) {
            cancellationToken.ThrowIfCancellationRequested();
            var wanted = (int)Math.Min(
                buffer.Length,
                writeLength - totalRead);
            await input.ReadExactlyAsync(
                    buffer.AsMemory(0, wanted),
                    cancellationToken)
                .ConfigureAwait(false);
            await _device.WriteAsync(
                    sector,
                    buffer.AsMemory(0, wanted),
                    cancellationToken)
                .ConfigureAwait(false);
            totalRead += wanted;
            if (_progress is not null) {
                await _progress(totalRead, writeLength)
                    .ConfigureAwait(false);
            }

            if (totalRead < writeLength) {
                sector = checked(
                    sector +
                    (uint)(wanted /
                        RockUsbProtocol.SectorSize));
            }
        }
    }

    private async Task BackupAsync(
        uint sector,
        long length,
        string backupPath,
        CancellationToken cancellationToken
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        await using var backup = new FileStream(
            backupPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            IoBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var remaining = length;
        while (remaining > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            var sectorCount = (ushort)Math.Min(
                remaining / RockUsbProtocol.SectorSize,
                IoBufferSize /
                    RockUsbProtocol.SectorSize);
            var data = await _device.ReadAsync(
                    sector,
                    sectorCount,
                    cancellationToken)
                .ConfigureAwait(false);
            await backup.WriteAsync(data, cancellationToken)
                .ConfigureAwait(false);
            sector = checked(sector + sectorCount);
            remaining -= data.Length;
        }

        await backup.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateLength(
        long length,
        uint startSector
    ) {
        if (length == 0 ||
            length % RockUsbProtocol.SectorSize != 0) {
            throw new InvalidDataException(
                "The write range must contain at least one complete " +
                "512-byte sector.");
        }

        var sectorCount =
            (ulong)length / RockUsbProtocol.SectorSize;
        if (sectorCount >
            (ulong)uint.MaxValue + 1 - startSector) {
            throw new InvalidDataException(
                "The write range exceeds the 32-bit RockUSB " +
                "sector address space.");
        }
    }

    private static void EnsureBackupDoesNotOverwriteSource(
        string sourcePath,
        string? backupPath
    ) {
        if (backupPath is null) {
            return;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (Path.GetFullPath(sourcePath).Equals(
                Path.GetFullPath(backupPath),
                comparison)) {
            throw new ArgumentException(
                "The backup path must differ from the input file.",
                nameof(backupPath));
        }
    }
}
