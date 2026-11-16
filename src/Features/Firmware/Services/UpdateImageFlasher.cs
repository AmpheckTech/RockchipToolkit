using System.Text;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.Features.Firmware.Model;
using Ampheck.RockchipToolkit.Images.Rkaf;
using Ampheck.RockchipToolkit.Images.Update;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Firmware.Services;

internal sealed class UpdateImageFlasher(FirmwareFlashContext context) {
    public async Task FlashAsync(
        string sourceLocation,
        CancellationToken cancellationToken
    ) {
        await using var input = await InputFile.OpenAsync(
                sourceLocation,
                context.Files,
                cancellationToken)
            .ConfigureAwait(false);
        using var update = RockchipUpdateImage.Open(input.Path);
        await context.Files.Report(
                $"Opened update image {Path.GetFileName(input.Path)}")
            .ConfigureAwait(false);
        context.Files.Logger.LogInformation(
            "Update image: model={MachineModel}, " +
            "manufacturer={Manufacturer}, version=0x{Version:X8}",
            update.MachineModel,
            update.Manufacturer,
            update.Version);
        await context.Files.Report(
                $"Package: {update.MachineModel}, " +
                $"{update.Manufacturer}, version 0x{update.Version:X8}")
            .ConfigureAwait(false);

        var parameterEntry = update.FindEntry("parameter") ??
            throw new InvalidDataException(
                "The update image contains no parameter entry.");
        var parameter = Encoding.UTF8.GetString(
                update.ReadParameterBytes(parameterEntry))
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
                context.Files.Logger.LogInformation(
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
        await context.Files.Report(
                $"Ready to flash {images.Count} partition image(s), " +
                $"{FormatBytes(totalBytes)} total")
            .ConfigureAwait(false);
        foreach (var (partition, entry) in images) {
            await context.Files.Report(
                    $"{entry.FileName}: partition {partition.Name}, " +
                    $"sector 0x{partition.Offset:X8}, " +
                    FormatBytes(entry.Size))
                .ConfigureAwait(false);
            context.Files.Logger.LogInformation(
                "Flashing partition {PartitionName} at 0x{Offset:X8} " +
                "from {ImageName}",
                partition.Name,
                partition.Offset,
                entry.FileName);
            await using var image = update.OpenEntryStream(entry);
            await CreateWriter(completedBytes, totalBytes)
                .WriteAsync(
                    image,
                    $"{entry.FileName} ({entry.Name})",
                    new DeviceWriteOptions(
                        partition.Offset,
                        EraseBeforeWrite: true),
                    cancellationToken)
                .ConfigureAwait(false);
            completedBytes += entry.Size;
        }

        await context.Files.Report("Update image written")
            .ConfigureAwait(false);
    }

    private DeviceWriter CreateWriter(
        long completedBytes,
        long totalBytes
    ) {
        Func<long, long, ValueTask>? progress =
            context.Files.Progress is { } report
            ? (written, _) => report(
                completedBytes + written,
                totalBytes)
            : null;
        return context.CreateWriter(progress);
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
