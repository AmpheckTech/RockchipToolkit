using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Core.Ports.Files;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.Features.Firmware.Model;
using Ampheck.RockchipToolkit.Features.Firmware.Services;
using Ampheck.RockchipToolkit.Features.Storage.Model;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Storage.Services;

internal sealed partial class StorageService {
    public async ValueTask<StorageWriteResult> WriteAsync(
        StorageWriteParams parameters,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        var logger = loggerFactory.CreateLogger<StorageService>();
        ValueTask ReportInputProgress(
            long current,
            long total) =>
            ReportProgressAsync(
                progress,
                "input",
                current,
                total);
        var fileContext = new InputFileContext(
            logger,
            message => {
                progress.ReportStatus(message);
                return ValueTask.CompletedTask;
            },
            ReportInputProgress,
            limits,
            remoteFiles);
        await using var input = await InputFile.OpenAsync(
                parameters.InputLocation,
                fileContext,
                cancellationToken)
            .ConfigureAwait(false);
        var sourceOffset = parameters.SourceOffset ?? 0;
        var inputLength = new FileInfo(input.Path).Length;
        var length = parameters.Length ??
            checked(inputLength - sourceOffset);
        ValidateWrite(inputLength, sourceOffset, length);
        var backupPath = parameters.BackupPath is null
            ? null
            : Path.GetFullPath(parameters.BackupPath);
        if (backupPath is not null) {
            var backupDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDirectory)) {
                Directory.CreateDirectory(backupDirectory);
            }
        }

        await using var lease = await access.OpenAsync(
                parameters.Target,
                parameters.LoaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await new DeviceWriter(
                lease.Device,
                logger,
                message => {
                    progress.ReportStatus(message);
                    return ValueTask.CompletedTask;
                },
                (current, total) => ReportProgressAsync(
                    progress,
                    "write",
                    current,
                    total),
                (current, total) => ReportProgressAsync(
                    progress,
                    "erase",
                    current,
                    total))
            .WriteAsync(
                input.Path,
                new DeviceWriteOptions(
                    parameters.Sector,
                    backupPath,
                    parameters.EraseBeforeWrite,
                    length,
                    sourceOffset),
                cancellationToken)
            .ConfigureAwait(false);
        return new StorageWriteResult {
            BytesWritten = length,
            BackupPath = backupPath
        };
    }

    private static ValueTask ReportProgressAsync(
        IProgress<OperationProgress>? progress,
        string label,
        long current,
        long total
    ) {
        progress.ReportProgress(label, current, total);
        return ValueTask.CompletedTask;
    }
}
