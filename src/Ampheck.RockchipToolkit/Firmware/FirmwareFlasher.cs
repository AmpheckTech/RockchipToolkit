using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Firmware.Internal;
using Ampheck.RockchipToolkit.Images.Update;
using Ampheck.RockchipToolkit.Protocol.RockUsb;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Firmware;

public sealed class FirmwareFlasher {
    private readonly DeviceAccess _access;
    private readonly ToolkitTransferLimits _limits;

    public FirmwareFlasher(
        IUsbDeviceProviderFactory hardware,
        ToolkitTransferLimits? limits = null
    ) {
        ArgumentNullException.ThrowIfNull(hardware);
        limits ??= ToolkitTransferLimits.Default;
        limits.Validate();
        var connector = new DeviceConnector(hardware);
        _access = new DeviceAccess(
            new RockchipBootLoader(),
            connector);
        _limits = limits;
    }

    public async ValueTask FlashAsync(
        DeviceTarget target,
        string firmwarePath,
        FirmwareFlashOptions? options,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(firmwarePath)) {
            throw new InvalidDataException(
                "The flash request contains no firmware source.");
        }

        options ??= new FirmwareFlashOptions();

        var inputName = Path.GetFileName(firmwarePath);
        progress?.Report(new OperationProgress(
            ToolkitOperationStage.Preparing,
            inputName,
            Completed: 0,
            Total: 0));
        ValueTask ReportFirmwareProgress(
            long current,
            long total) =>
            ReportProgressAsync(
                progress,
                ToolkitOperationStage.Writing,
                inputName,
                current,
                total);
        ValueTask ReportEraseProgress(
            long current,
            long total) =>
            ReportProgressAsync(
                progress,
                ToolkitOperationStage.Erasing,
                inputName,
                current,
                total);
        await using var input = await FlashInput.OpenAsync(
                firmwarePath,
                _limits,
                cancellationToken)
            .ConfigureAwait(false);
        if (input.SourceKind == FlashSourceKind.UpdateImage) {
            progress?.Report(new OperationProgress(
                ToolkitOperationStage.Verifying,
                inputName,
                Completed: 0,
                Total: 0));
            var verificationProgress = progress is null
                ? null
                : new FirmwareImageVerificationProgress(
                    progress,
                    inputName);
            await RockchipImageVerifier.VerifyAsync(
                    input.Location,
                    verificationProgress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await using var lease = await _access.OpenAsync(
                target,
                options.LoaderPath,
                progress,
                cancellationToken,
                useEmbeddedLoader:
                    input.SourceKind == FlashSourceKind.UpdateImage,
                firmwarePath: input.Location)
            .ConfigureAwait(false);
        var flashContext = new FirmwareFlashContext(
            lease.Device,
            progress,
            ReportFirmwareProgress,
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

        if (options.ResetAfterFlash) {
            progress?.Report(new OperationProgress(
                ToolkitOperationStage.Resetting,
                inputName,
                Completed: 0,
                Total: 0));
            await lease.Device.ResetAsync(
                    ResetMode.Normal,
                    cancellationToken)
                .ConfigureAwait(false);
        }
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
