using System.Text;
using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Protocol;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal static class UpdateImageFlasher {
    public static async Task FlashAsync(
        RockchipDevice device,
        string sourceLocation,
        bool resetAfterFlash,
        ILogger logger,
        Func<string, ValueTask> report,
        Func<long, long, ValueTask>? progress,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        await using var input = await InputFile.OpenAsync(
                sourceLocation,
                logger,
                report,
                progress,
                cancellationToken,
                limits: limits)
            .ConfigureAwait(false);
        using var update = RockchipUpdateImage.Open(input.Path);
        await report(
                $"Opened update image {Path.GetFileName(input.Path)}")
            .ConfigureAwait(false);
        logger.LogInformation(
            "Update image: model={MachineModel}, " +
            "manufacturer={Manufacturer}, version=0x{Version:X8}",
            update.MachineModel,
            update.Manufacturer,
            update.Version);
        await report(
                $"Package: {update.MachineModel}, " +
                $"{update.Manufacturer}, version 0x{update.Version:X8}")
            .ConfigureAwait(false);

        var parameterEntry = update.FindEntry("parameter") ??
            throw new InvalidDataException(
                "The update image contains no parameter entry.");
        var parameter = Encoding.UTF8.GetString(
                update.ReadEntryBytes(parameterEntry))
            .TrimEnd('\0');
        var partitions = RockchipParameterParser.Parse(parameter);
        var images = new List<(
            RockchipPartition Partition,
            RockchipUpdateEntry Entry)>();
        foreach (var partition in partitions) {
            var entry = update.FindEntry(partition.Name);
            if (entry is null ||
                entry.Size == 0 ||
                entry.FileName.Equals(
                    "RESERVED",
                    StringComparison.OrdinalIgnoreCase)) {
                logger.LogInformation(
                    "Skipping partition {PartitionName}: " +
                    "the update image contains no data",
                    partition.Name);
                continue;
            }

            FirmwareImageValidator.Validate(
                partition,
                entry.FileName,
                entry.Size);
            images.Add((partition, entry));
        }

        if (images.Count == 0) {
            throw new InvalidDataException(
                "The update image contains no writable partition images.");
        }

        var totalBytes = images.Sum(image => image.Entry.Size);
        long completedBytes = 0;
        await report(
                $"Ready to flash {images.Count} partition image(s), " +
                $"{FormatBytes(totalBytes)} total")
            .ConfigureAwait(false);
        foreach (var (partition, entry) in images) {
            await report(
                    $"{entry.FileName}: partition {partition.Name}, " +
                    $"sector 0x{partition.Offset:X8}, " +
                    FormatBytes(entry.Size))
                .ConfigureAwait(false);
            logger.LogInformation(
                "Flashing partition {PartitionName} at 0x{Offset:X8} " +
                "from {ImageName}",
                partition.Name,
                partition.Offset,
                entry.FileName);
            await using var image =
                update.OpenEntryStream(entry);
            await FirmwareWriter.WriteAsync(
                    device,
                    image,
                    $"{entry.FileName} ({entry.Name})",
                    partition.Offset,
                    backupPath: null,
                    eraseBeforeWrite: true,
                    logger,
                    report,
                    progress is null
                        ? null
                        : (written, _) => progress(
                            completedBytes + written,
                            totalBytes))
                .ConfigureAwait(false);
            completedBytes += entry.Size;
        }

        if (resetAfterFlash) {
            await report("Resetting device").ConfigureAwait(false);
            await device.ResetAsync(ResetMode.Normal)
                .ConfigureAwait(false);
            await report("Update image flash complete; device reset")
                .ConfigureAwait(false);
        } else {
            await report("Update image flash complete")
                .ConfigureAwait(false);
        }
    }

    private static string FormatBytes(long bytes) {
        return bytes switch {
            >= 1 << 30 => $"{bytes / (double)(1 << 30):F1} GiB",
            >= 1 << 20 => $"{bytes / (double)(1 << 20):F1} MiB",
            >= 1 << 10 => $"{bytes / (double)(1 << 10):F1} KiB",
            _ => $"{bytes} B"
        };
    }
}
