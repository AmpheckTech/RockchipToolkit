using System.Text;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Images.Rkaf;
using Ampheck.RockchipToolkit.Images.Update;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class UpdateImageFlasher(FirmwareFlashContext context) {
    public async Task FlashAsync(
        string sourceLocation,
        CancellationToken cancellationToken
    ) {
        await using var input = await InputFile.OpenAsync(
                sourceLocation,
                cancellationToken)
            .ConfigureAwait(false);
        using var update = RockchipUpdateImage.Open(input.Path);

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
        foreach (var (partition, entry) in images) {
            context.Status?.Report(new OperationProgress(
                ToolkitOperationStage.Writing,
                entry.FileName,
                Completed: 0,
                Total: 0));
            await using var image = update.OpenEntryStream(entry);
            await CreateWriter(completedBytes, totalBytes)
                .WriteAsync(
                    image,
                    new DeviceWriteOptions(
                        partition.Offset,
                        EraseBeforeWrite: true),
                    cancellationToken)
                .ConfigureAwait(false);
            completedBytes += entry.Size;
        }
    }

    private DeviceWriter CreateWriter(
        long completedBytes,
        long totalBytes
    ) {
        Func<long, long, ValueTask>? progress =
            context.Progress is { } report
            ? (written, _) => report(
                completedBytes + written,
                totalBytes)
            : null;
        return context.CreateWriter(progress);
    }
}
