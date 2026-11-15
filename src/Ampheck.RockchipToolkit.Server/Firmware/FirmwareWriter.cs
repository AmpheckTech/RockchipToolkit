using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Protocol;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed class FirmwareWriter(
    RockchipDevice device,
    ILogger logger,
    Func<string, ValueTask>? report = null,
    Func<long, long, ValueTask>? progress = null,
    Func<long, long, ValueTask>? eraseProgress = null) {
    private const int IoBufferSize = 1 << 20;

    public async Task WriteAsync(
        string path,
        FirmwareWriteOptions options,
        CancellationToken cancellationToken = default) {
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
                path,
                options,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task WriteAsync(
        Stream input,
        string sourceName,
        FirmwareWriteOptions options,
        CancellationToken cancellationToken = default) {
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
            await ReportAsync(
                    $"Backing up {writeLength:N0} bytes to " +
                    Path.GetFileName(options.BackupPath))
                .ConfigureAwait(false);
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
            await ReportAsync(
                    $"Erasing {sectorCount:N0} sectors at " +
                    $"0x{options.Sector:X8}")
                .ConfigureAwait(false);
            await device.EraseAsync(
                    options.Sector,
                    sectorCount,
                    eraseProgress,
                    cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Erased {SectorCount} sector(s) before writing",
                sectorCount);
        }

        logger.LogInformation(
            "Writing {ByteCount} bytes from {SourceName} " +
            "at sector 0x{StartSector:X8}, source offset 0x{SourceOffset:X8}",
            writeLength,
            sourceName,
            options.Sector,
            offset);
        await ReportAsync(
                $"Writing {writeLength:N0} bytes at " +
                $"0x{options.Sector:X8}")
            .ConfigureAwait(false);
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
            await device.WriteAsync(
                    sector,
                    buffer.AsMemory(0, wanted),
                    cancellationToken)
                .ConfigureAwait(false);
            totalRead += wanted;
            if (progress is not null) {
                await progress(totalRead, writeLength)
                    .ConfigureAwait(false);
            }

            if (totalRead < writeLength) {
                sector = checked(
                    sector +
                    (uint)(wanted /
                        RockUsbProtocol.SectorSize));
            }
        }

        await ReportAsync(
                $"Wrote {totalRead:N0} bytes from " +
                Path.GetFileName(sourceName))
            .ConfigureAwait(false);
    }

    private async Task BackupAsync(
        uint sector,
        long length,
        string backupPath,
        CancellationToken cancellationToken) {
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
            var data = await device.ReadAsync(
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
        logger.LogInformation(
            "Backed up {ByteCount} bytes to {BackupPath}",
            length,
            backupPath);
    }

    private static void ValidateLength(
        long length,
        uint startSector) {
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
        string? backupPath) {
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

    private ValueTask ReportAsync(string message) {
        return report is null
            ? ValueTask.CompletedTask
            : report(message);
    }
}
