using Ampheck.RockchipToolkit.Core.Limits;
using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Core.Ports.Files;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.Features.Firmware.Model;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Firmware.Services;

internal sealed partial class FirmwareService(
    DeviceAccess access,
    IRemoteFileClient remoteFiles,
    ToolkitTransferLimits limits,
    ILoggerFactory loggerFactory
) {

    public async ValueTask FlashAsync(
        FirmwareFlashParams parameters,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        var location = parameters.FirmwareLocation;
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The flash request contains no firmware source.");
        }

        var logger = loggerFactory.CreateLogger<FirmwareService>();
        ValueTask ReportFirmwareProgress(
            long current,
            long total) =>
            ReportProgressAsync(
                progress,
                "firmware",
                current,
                total);
        ValueTask ReportEraseProgress(
            long current,
            long total) =>
            ReportProgressAsync(
                progress,
                "erase",
                current,
                total);
        var fileContext = new InputFileContext(
            logger,
            message => {
                progress.ReportStatus(message);
                return ValueTask.CompletedTask;
            },
            ReportFirmwareProgress,
            limits,
            remoteFiles);
        await using var input = await FlashInput.OpenAsync(
                location,
                fileContext,
                cancellationToken)
            .ConfigureAwait(false);
        await using var lease = await access.OpenAsync(
                parameters.Target,
                parameters.LoaderLocation,
                progress,
                cancellationToken,
                useEmbeddedLoader:
                    input.SourceKind == FlashSourceKind.UpdateImage,
                firmwarePath: input.Location)
            .ConfigureAwait(false);
        var flashContext = new FirmwareFlashContext(
            lease.Device,
            fileContext,
            ReportEraseProgress);
        if (input.SourceKind == FlashSourceKind.UpdateImage) {
            await new UpdateImageFlasher(flashContext).FlashAsync(
                    input.Location,
                    cancellationToken)
                .ConfigureAwait(false);
        } else {
            await new DirectoryFlasher(flashContext).FlashAsync(
                    input.Location,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (parameters.ResetAfterFlash) {
            progress.ReportStatus("Resetting device");
            await lease.Device.ResetAsync(ResetMode.Normal)
                .ConfigureAwait(false);
        }

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
