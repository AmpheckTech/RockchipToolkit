using System.Text;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Images.Rkaf;
using Ampheck.RockchipToolkit.Images.Update;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class UpdateImageFlasher(FirmwareFlashContext context) {
    public async Task FlashAsync(
        string sourceLocation,
        string? bootloaderPath,
        bool usePackageBootloader,
        byte[]? idb,
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
        if (idb is not null) {
            FirmwareImageValidator.ValidateBootloader(
                "IDB",
                idb.Length,
                partitions[0].Offset);
        }

        InputFile? explicitBootloader = null;
        RockchipUpdateEntry? bootloaderEntry = null;
        string? bootloaderName = null;
        long bootloaderLength = 0;
        try {
            if (bootloaderPath is not null) {
                explicitBootloader = await InputFile.OpenAsync(
                        bootloaderPath,
                        cancellationToken)
                    .ConfigureAwait(false);
                bootloaderName = Path.GetFileName(explicitBootloader.Path);
                bootloaderLength = new FileInfo(
                        explicitBootloader.Path)
                    .Length;
            } else if (usePackageBootloader) {
                bootloaderEntry = update.FindEntry("bootloader");
                if (bootloaderEntry is not null &&
                    (bootloaderEntry.Size == 0 ||
                        bootloaderEntry.FileName.Equals(
                            "RESERVED",
                            StringComparison.OrdinalIgnoreCase) ||
                        bootloaderEntry.FileName.Equals(
                            "SELF",
                            StringComparison.OrdinalIgnoreCase))) {
                    bootloaderEntry = null;
                }

                if (bootloaderEntry is not null) {
                    bootloaderName = bootloaderEntry.FileName;
                    bootloaderLength = bootloaderEntry.Size;
                }
            }

            if (bootloaderLength > 0) {
                FirmwareImageValidator.ValidateBootloader(
                    bootloaderName!,
                    bootloaderLength,
                    partitions[0].Offset);
            }

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

            if (images.Count == 0 && bootloaderLength == 0) {
                throw new InvalidDataException(
                    "The update image contains no writable partition images.");
            }

            var totalBytes = images.Sum(image => image.Entry.Size) +
                bootloaderLength +
                (idb?.Length ?? 0);
            long completedBytes = 0;
            if (idb is not null) {
                context.Status?.Report(new OperationProgress(
                    ToolkitOperationStage.Writing,
                    "IDB",
                    Completed: 0,
                    Total: 0));
                await context.Device.WriteAsync(
                        PersistentLoader.IdbSector,
                        idb,
                        cancellationToken)
                    .ConfigureAwait(false);
                completedBytes += idb.Length;
            }

            if (bootloaderLength > 0) {
                context.Status?.Report(new OperationProgress(
                    ToolkitOperationStage.Writing,
                    bootloaderName!,
                    Completed: 0,
                    Total: 0));
                await using var image = explicitBootloader is null
                    ? update.OpenEntryStream(bootloaderEntry!)
                    : new FileStream(
                        explicitBootloader.Path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        1024 * 1024,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);
                await CreateWriter(completedBytes, totalBytes)
                    .WriteAsync(
                        image,
                        new DeviceWriteOptions(
                            FirmwareImageValidator.BootloaderSector,
                            EraseBeforeWrite: true),
                        cancellationToken)
                    .ConfigureAwait(false);
                completedBytes += bootloaderLength;
            }

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
        } finally {
            if (explicitBootloader is not null) {
                await explicitBootloader.DisposeAsync().ConfigureAwait(false);
            }
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
