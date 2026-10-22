using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Images;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal static class DirectoryFlasher {
    public static async Task FlashAsync(
        RockchipDevice device,
        string sourceLocation,
        ILogger logger,
        Func<string, ValueTask> report,
        Func<long, long, ValueTask>? progress,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        var source = FirmwareSource.Create(sourceLocation);
        var partitions = await LoadPartitionsAsync(
                source,
                logger,
                report,
                progress,
                limits,
                cancellationToken)
            .ConfigureAwait(false);
        var images = new List<(
            RockchipPartition Partition,
            string ImageName,
            InputFile File,
            long Length)>();
        try {
            foreach (var partition in partitions) {
                var imageName = $"{partition.Name}.img";
                var image = await source.TryOpenAsync(
                        imageName,
                        logger,
                        report,
                        progress,
                        cancellationToken,
                        limits: limits)
                    .ConfigureAwait(false);
                if (image is null) {
                    logger.LogInformation(
                        "Skipping partition {PartitionName}: " +
                        "{ImageName} is absent",
                        partition.Name,
                        imageName);
                    continue;
                }

                var imageLength = new FileInfo(image.Path).Length;
                try {
                    FirmwareImageValidator.Validate(
                        partition,
                        imageName,
                        imageLength);
                    images.Add((
                        partition,
                        imageName,
                        image,
                        imageLength));
                } catch {
                    await image.DisposeAsync()
                        .ConfigureAwait(false);
                    throw;
                }
            }

            if (images.Count == 0) {
                throw new InvalidDataException(
                    "The firmware directory contains no partition images " +
                    "listed by parameter.txt.");
            }

            var totalBytes = images.Sum(image => image.Length);
            long completedBytes = 0;
            await report(
                    $"Ready to flash {images.Count} partition image(s), " +
                    $"{FormatBytes(totalBytes)} total")
                .ConfigureAwait(false);
            foreach (var (partition, imageName, image, imageLength) in images) {
                await report(
                        $"{imageName}: partition {partition.Name}, " +
                        $"sector 0x{partition.Offset:X8}, " +
                        FormatBytes(imageLength))
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Flashing partition {PartitionName} at 0x{Offset:X8} " +
                    "from {ImageName}",
                    partition.Name,
                    partition.Offset,
                    imageName);
                await FirmwareWriter.WriteAsync(
                        device,
                        image.Path,
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
                completedBytes += imageLength;
            }
        } finally {
            foreach (var (_, _, image, _) in images) {
                await image.DisposeAsync().ConfigureAwait(false);
            }
        }

        await report("Firmware directory written")
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<RockchipPartition>>
        LoadPartitionsAsync(
            FirmwareSource source,
            ILogger logger,
            Func<string, ValueTask> report,
            Func<long, long, ValueTask>? progress,
            ToolkitTransferLimits limits,
            CancellationToken cancellationToken) {
        await using var parameterFile = await InputFile.OpenAsync(
                source.GetLocation("parameter.txt"),
                logger,
                report,
                progress,
                cancellationToken,
                limits: limits)
            .ConfigureAwait(false);
        var parameter = await File.ReadAllTextAsync(
                parameterFile.Path,
                cancellationToken)
            .ConfigureAwait(false);
        return RockchipParameterParser.Parse(parameter);
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
