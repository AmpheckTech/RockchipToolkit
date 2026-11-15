using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Images;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed class DirectoryFlasher(FirmwareFlashContext context) {
    public async Task FlashAsync(
        string sourceLocation,
        CancellationToken cancellationToken) {
        var source = FirmwareSource.Create(sourceLocation);
        var partitions = await LoadPartitionsAsync(
                source,
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
                var image = await context.Files.TryOpenSourceAsync(
                        source,
                        imageName,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (image is null) {
                    context.Files.Logger.LogInformation(
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
            await context.Files.Report(
                    $"Ready to flash {images.Count} partition image(s), " +
                    $"{FormatBytes(totalBytes)} total")
                .ConfigureAwait(false);
            foreach (var (partition, imageName, image, imageLength) in images) {
                await context.Files.Report(
                        $"{imageName}: partition {partition.Name}, " +
                        $"sector 0x{partition.Offset:X8}, " +
                        FormatBytes(imageLength))
                    .ConfigureAwait(false);
                context.Files.Logger.LogInformation(
                    "Flashing partition {PartitionName} at 0x{Offset:X8} " +
                    "from {ImageName}",
                    partition.Name,
                    partition.Offset,
                    imageName);
                await CreateWriter(completedBytes, totalBytes)
                    .WriteAsync(
                        image.Path,
                        new FirmwareWriteOptions(
                            partition.Offset,
                            EraseBeforeWrite: true),
                        cancellationToken)
                    .ConfigureAwait(false);
                completedBytes += imageLength;
            }
        } finally {
            foreach (var (_, _, image, _) in images) {
                await image.DisposeAsync().ConfigureAwait(false);
            }
        }

        await context.Files.Report("Firmware directory written")
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<RockchipPartition>>
        LoadPartitionsAsync(
            FirmwareSource source,
            CancellationToken cancellationToken) {
        await using var parameterFile = await InputFile.OpenAsync(
                source.GetLocation("parameter.txt"),
                context.Files,
                cancellationToken)
            .ConfigureAwait(false);
        var parameter = await File.ReadAllTextAsync(
                parameterFile.Path,
                cancellationToken)
            .ConfigureAwait(false);
        return RockchipParameterParser.Parse(parameter);
    }

    private FirmwareWriter CreateWriter(
        long completedBytes,
        long totalBytes) {
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
