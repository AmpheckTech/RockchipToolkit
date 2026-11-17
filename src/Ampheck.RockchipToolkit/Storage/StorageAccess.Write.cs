using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Firmware.Internal;

namespace Ampheck.RockchipToolkit.Storage;

public sealed partial class StorageAccess {
    public async ValueTask<long> WriteAsync(
        DeviceTarget target,
        string? loaderPath,
        string inputPath,
        uint sector,
        StorageWriteOptions? options,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        options ??= new StorageWriteOptions();
        await using var input = await InputFile.OpenAsync(
                inputPath,
                cancellationToken)
            .ConfigureAwait(false);
        var sourceOffset = options.SourceOffset ?? 0;
        var inputName = Path.GetFileName(input.Path);
        var inputLength = new FileInfo(input.Path).Length;
        var length = options.Length ??
            checked(inputLength - sourceOffset);
        ValidateWrite(inputLength, sourceOffset, length);
        var backupPath = options.BackupPath is null
            ? null
            : Path.GetFullPath(options.BackupPath);
        if (backupPath is not null) {
            var backupDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDirectory)) {
                Directory.CreateDirectory(backupDirectory);
            }
        }

        await using var lease = await _access.OpenAsync(
                target,
                loaderPath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await new DeviceWriter(
                lease.Device,
                (current, total) => ReportProgressAsync(
                    progress,
                    ToolkitOperationStage.Writing,
                    inputName,
                    current,
                    total),
                (current, total) => ReportProgressAsync(
                    progress,
                    ToolkitOperationStage.Erasing,
                    inputName,
                    current,
                    total))
            .WriteAsync(
                input.Path,
                new DeviceWriteOptions(
                    sector,
                    backupPath,
                    options.EraseBeforeWrite,
                    length,
                    sourceOffset),
                cancellationToken)
            .ConfigureAwait(false);
        return length;
    }

    private static ValueTask ReportProgressAsync(
        IProgress<OperationProgress>? progress,
        ToolkitOperationStage stage,
        string item,
        long current,
        long total
    ) {
        progress?.Report(new OperationProgress(
            stage,
            item,
            Completed: current,
            Total: total));
        return ValueTask.CompletedTask;
    }
}
