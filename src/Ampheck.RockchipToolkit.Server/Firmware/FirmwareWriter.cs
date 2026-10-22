using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Protocol;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal static class FirmwareWriter {
    private const int IoBufferSize = 1 << 20;

    public static async Task WriteAsync(
        RockchipDevice device,
        string path,
        uint sector,
        string? backupPath,
        bool eraseBeforeWrite,
        ILogger logger,
        Func<string, ValueTask>? report = null,
        Func<long, long, ValueTask>? progress = null,
        long? length = null,
        long? sourceOffset = null) {
        EnsureBackupDoesNotOverwriteSource(path, backupPath);
        await using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            IoBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await WriteAsync(
                device,
                input,
                path,
                sector,
                backupPath,
                eraseBeforeWrite,
                logger,
                report,
                progress,
                length,
                sourceOffset)
            .ConfigureAwait(false);
    }

    public static async Task WriteAsync(
        RockchipDevice device,
        Stream input,
        string sourceName,
        uint sector,
        string? backupPath,
        bool eraseBeforeWrite,
        ILogger logger,
        Func<string, ValueTask>? report = null,
        Func<long, long, ValueTask>? progress = null,
        long? length = null,
        long? sourceOffset = null) {
        if (!input.CanRead || !input.CanSeek) {
            throw new ArgumentException(
                "The input stream must be readable and seekable.",
                nameof(input));
        }

        var offset = sourceOffset ?? 0;
        if (offset < 0 || offset >= input.Length &&
            input.Length != 0) {
            throw new InvalidDataException(
                "The source offset is outside the input file.");
        }

        var remainingLength = input.Length - offset;
        var writeLength = length ?? remainingLength;
        if (writeLength < 0 || writeLength > remainingLength) {
            throw new InvalidDataException(
                "The requested write length exceeds the input file.");
        }

        input.Position = offset;
        ValidateLength(writeLength, sector);
        if (backupPath is not null) {
            await ReportAsync(
                    report,
                    $"Backing up {writeLength:N0} bytes to " +
                    Path.GetFileName(backupPath))
                .ConfigureAwait(false);
            await BackupAsync(
                    device,
                    sector,
                    writeLength,
                    backupPath,
                    logger)
                .ConfigureAwait(false);
        }

        if (eraseBeforeWrite) {
            var sectorCount = checked(
                (uint)(writeLength /
                    RockUsbProtocol.SectorSize));
            await ReportAsync(
                    report,
                    $"Erasing {sectorCount:N0} sectors at " +
                    $"0x{sector:X8}")
                .ConfigureAwait(false);
            await device.EraseAsync(sector, sectorCount)
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
            sector,
            offset);
        await ReportAsync(
                report,
                $"Writing {writeLength:N0} bytes at 0x{sector:X8}")
            .ConfigureAwait(false);
        var buffer = new byte[IoBufferSize];
        long totalRead = 0;
        while (totalRead < writeLength) {
            var wanted = (int)Math.Min(
                buffer.Length,
                writeLength - totalRead);
            await input.ReadExactlyAsync(
                    buffer.AsMemory(0, wanted))
                .ConfigureAwait(false);
            await device.WriteAsync(
                    sector,
                    buffer.AsMemory(0, wanted))
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
                report,
                $"Wrote {totalRead:N0} bytes from " +
                Path.GetFileName(sourceName))
            .ConfigureAwait(false);
    }

    private static async Task BackupAsync(
        RockchipDevice device,
        uint sector,
        long length,
        string backupPath,
        ILogger logger) {
        await using var backup = new FileStream(
            backupPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            IoBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var remaining = length;
        while (remaining > 0) {
            var sectorCount = (ushort)Math.Min(
                remaining / RockUsbProtocol.SectorSize,
                IoBufferSize /
                    RockUsbProtocol.SectorSize);
            var data = await device.ReadAsync(
                    sector,
                    sectorCount)
                .ConfigureAwait(false);
            await backup.WriteAsync(data).ConfigureAwait(false);
            sector = checked(sector + sectorCount);
            remaining -= data.Length;
        }

        await backup.FlushAsync().ConfigureAwait(false);
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

    private static ValueTask ReportAsync(
        Func<string, ValueTask>? report,
        string message) {
        return report is null
            ? ValueTask.CompletedTask
            : report(message);
    }
}
