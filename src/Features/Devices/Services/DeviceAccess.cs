using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.Core.Limits;
using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Core.Ports.Files;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Firmware.Model;
using Ampheck.RockchipToolkit.Features.Firmware.Services;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Devices.Services;

internal sealed class DeviceAccess(
    IRemoteFileClient remoteFiles,
    RockchipBootLoader loaderDownloader,
    ToolkitTransferLimits limits,
    DeviceConnector connector,
    ILoggerFactory loggerFactory
) {

    public async ValueTask<RockchipDeviceLease> OpenAsync(
        DeviceTarget target,
        string? loaderLocation,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken,
        bool useEmbeddedLoader = false,
        string? firmwarePath = null
    ) {
        var logger = loggerFactory.CreateLogger<DeviceAccess>();
        if (!string.IsNullOrWhiteSpace(loaderLocation)) {
            await DownloadLoaderAsync(
                    target,
                    loaderLocation,
                    logger,
                        progress,
                    cancellationToken)
                .ConfigureAwait(false);
        } else if (useEmbeddedLoader &&
            firmwarePath is not null) {
            var mode = await connector.GetModeAsync(
                    target,
                    loggerFactory,
                    cancellationToken)
                .ConfigureAwait(false);
            if (mode == RockchipUsbMode.MaskRom) {
                logger.LogInformation(
                    "MaskROM detected; using the update image loader");
                await DownloadLoaderAsync(
                        target,
                        firmwarePath,
                        logger,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var session = await connector.OpenAsync(
                target,
                loggerFactory,
                logger,
                cancellationToken)
            .ConfigureAwait(false);
        var device = new RockchipDevice(
            session.Transport,
            loaderDownloader,
            loggerFactory: loggerFactory);
        return new RockchipDeviceLease(
            device,
            session.Provider);
    }

    private async Task DownloadLoaderAsync(
        DeviceTarget target,
        string location,
        ILogger logger,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The loader input is missing.");
        }

        ValueTask ReportLoaderProgress(
            long current,
            long total) =>
            ReportProgressAsync(
                progress,
                current,
                total);
        var fileContext = new InputFileContext(
            logger,
            message => {
                progress.ReportStatus(message);
                return ValueTask.CompletedTask;
            },
            ReportLoaderProgress,
            limits,
            remoteFiles);
        await using var input = await InputFile.OpenAsync(
                location,
                fileContext,
                cancellationToken)
            .ConfigureAwait(false);
        var session = await connector.OpenAsync(
                target,
                loggerFactory,
                logger,
                cancellationToken)
            .ConfigureAwait(false);
        await using (session.Provider) {
            await using var device = new RockchipDevice(
                session.Transport,
                loaderDownloader,
                loggerFactory: loggerFactory);
            await device.DownloadBootAsync(
                    input.Path,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        logger.LogInformation("Loader prepared for command execution");
        await WaitForLoaderModeAsync(
                target,
                logger,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask ReportProgressAsync(
        IProgress<OperationProgress>? progress,
        long current,
        long total
    ) {
        progress.ReportProgress("loader", current, total);
        return ValueTask.CompletedTask;
    }

    private async ValueTask WaitForLoaderModeAsync(
        DeviceTarget target,
        ILogger logger,
        CancellationToken cancellationToken
    ) {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var sawMaskRom = false;
        while (DateTimeOffset.UtcNow < deadline) {
            RockchipUsbMode? mode;
            try {
                mode = await connector.GetModeAsync(
                        target,
                        loggerFactory,
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch (IOException) {
                mode = null;
            }

            if (mode == RockchipUsbMode.Loader) {
                return;
            }

            sawMaskRom |= mode == RockchipUsbMode.MaskRom;
            await Task.Delay(
                    TimeSpan.FromMilliseconds(200),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        logger.LogError(
            "Loader transfer finished but the device did not enter Loader mode");
        throw new InvalidOperationException(
            sawMaskRom
                ? "Loader transfer finished, but the device remained in " +
                  "MaskROM. Use a valid MaskROM loader for this SoC."
                : "Loader transfer finished, but the device did not " +
                  "re-enumerate in Loader mode.");
    }
}
