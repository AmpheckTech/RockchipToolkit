using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Firmware.Internal;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Devices.Internal;

internal sealed class DeviceAccess(
    RockchipBootLoader loaderDownloader,
    DeviceConnector connector
) {
    public async ValueTask<RockchipDeviceLease> OpenAsync(
        DeviceTarget target,
        string? loaderLocation,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken,
        bool useEmbeddedLoader = false,
        string? firmwarePath = null
    ) {
        if (!string.IsNullOrWhiteSpace(loaderLocation)) {
            await DownloadLoaderAsync(
                    target,
                    loaderLocation,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        } else if (useEmbeddedLoader &&
            firmwarePath is not null) {
            var mode = await connector.GetModeAsync(
                    target,
                    cancellationToken)
                .ConfigureAwait(false);
            if (mode == RockchipUsbMode.MaskRom) {
                await DownloadLoaderAsync(
                        target,
                        firmwarePath,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var session = await connector.OpenAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
        var device = new RockchipDevice(
            session.Transport,
            loaderDownloader);
        return new RockchipDeviceLease(
            device,
            session.Provider);
    }

    private async Task DownloadLoaderAsync(
        DeviceTarget target,
        string location,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The loader input is missing.");
        }

        progress?.Report(new OperationProgress(
            ToolkitOperationStage.LoadingLoader,
            Path.GetFileName(location),
            Completed: 0,
            Total: 0));
        await using var input = await InputFile.OpenAsync(
                location,
                cancellationToken)
            .ConfigureAwait(false);
        var session = await connector.OpenAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
        await using (session.Provider) {
            await using var device = new RockchipDevice(
                session.Transport,
                loaderDownloader);
            await device.DownloadBootAsync(
                    input.Path,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await WaitForLoaderModeAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask ReportProgressAsync(
        IProgress<OperationProgress>? progress,
        long current,
        long total
    ) {
        progress?.Report(new OperationProgress(
            ToolkitOperationStage.LoadingLoader,
            Item: null,
            Completed: current,
            Total: total));
        return ValueTask.CompletedTask;
    }

    private async ValueTask WaitForLoaderModeAsync(
        DeviceTarget target,
        CancellationToken cancellationToken
    ) {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        var sawMaskRom = false;
        while (DateTimeOffset.UtcNow < deadline) {
            RockchipUsbMode? mode;
            try {
                mode = await connector.GetModeAsync(
                        target,
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

        throw new InvalidOperationException(
            sawMaskRom
                ? "Loader transfer finished, but the device remained in " +
                  "MaskROM. Use a valid MaskROM loader for this SoC."
                : "Loader transfer finished, but the device did not " +
                  "re-enumerate in Loader mode.");
    }
}
