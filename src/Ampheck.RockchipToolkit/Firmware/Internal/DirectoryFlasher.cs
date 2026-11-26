using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Images.Rkaf;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class DirectoryFlasher(FirmwareFlashContext context) {
    public async Task FlashAsync(
        string sourceLocation,
        CancellationToken cancellationToken
    ) {
        var partitions = await LoadPartitionsAsync(
                sourceLocation,
                cancellationToken)
            .ConfigureAwait(false);
        var images = new List<(
            RockchipPartition Partition,
            string ImageName,
            InputFile File,
            long Length)>();
        InputFile? bootloader = null;
        long bootloaderLength = 0;
        try {
            bootloader = await OpenBootloaderAsync(
                    sourceLocation,
                    cancellationToken)
                .ConfigureAwait(false);
            if (bootloader is not null) {
                bootloaderLength = new FileInfo(bootloader.Path).Length;
                FirmwareImageValidator.ValidateBootloader(
                    Path.GetFileName(bootloader.Path),
                    bootloaderLength,
                    partitions[0].Offset);
            }

            foreach (var partition in partitions) {
                var imageName = $"{partition.Name}.img";
                var image = await FirmwareSource.Create(sourceLocation)
                    .TryOpenAsync(
                        imageName,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (image is null) {
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

            if (images.Count == 0 && bootloader is null) {
                throw new InvalidDataException(
                    "The firmware directory contains no partition images " +
                    "listed by parameter.txt.");
            }

            var totalBytes = images.Sum(image => image.Length) +
                bootloaderLength;
            long completedBytes = 0;
            if (bootloader is not null) {
                context.Status?.Report(new OperationProgress(
                    ToolkitOperationStage.Writing,
                    Path.GetFileName(bootloader.Path),
                    Completed: 0,
                    Total: 0));
                await CreateWriter(completedBytes, totalBytes)
                    .WriteAsync(
                        bootloader.Path,
                        new DeviceWriteOptions(
                            FirmwareImageValidator.BootloaderSector,
                            EraseBeforeWrite: true),
                        cancellationToken)
                    .ConfigureAwait(false);
                completedBytes += bootloaderLength;
            }

            foreach (var (partition, imageName, image, imageLength) in images) {
                context.Status?.Report(new OperationProgress(
                    ToolkitOperationStage.Writing,
                    imageName,
                    Completed: 0,
                    Total: 0));
                await CreateWriter(completedBytes, totalBytes)
                    .WriteAsync(
                        image.Path,
                        new DeviceWriteOptions(
                            partition.Offset,
                            EraseBeforeWrite: true),
                        cancellationToken)
                    .ConfigureAwait(false);
                completedBytes += imageLength;
            }
        } finally {
            if (bootloader is not null) {
                await bootloader.DisposeAsync().ConfigureAwait(false);
            }

            foreach (var (_, _, image, _) in images) {
                await image.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<InputFile?> OpenBootloaderAsync(
        string sourceLocation,
        CancellationToken cancellationToken
    ) {
        var source = FirmwareSource.Create(sourceLocation);
        return await source.TryOpenAsync(
                "idbloader.img",
                cancellationToken)
            .ConfigureAwait(false) ??
            await source.TryOpenAsync(
                    "bootloader.img",
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<RockchipPartition>>
        LoadPartitionsAsync(
            string sourceLocation,
            CancellationToken cancellationToken) {
        var source = FirmwareSource.Create(sourceLocation);
        await using var parameterFile =
            await source.TryOpenAsync(
                    "parameter.txt",
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new FileNotFoundException(
                $"Firmware input 'parameter.txt' was not found.",
                source.GetLocation("parameter.txt"));
        var parameter = await File.ReadAllTextAsync(
                parameterFile.Path,
                cancellationToken)
            .ConfigureAwait(false);
        return RockchipParameterParser.Parse(parameter);
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
