using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;
using Ampheck.RockchipToolkit.Server.Firmware;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Devices;

internal sealed class DeviceAccess(
    BlobStore blobs,
    ToolkitTransferLimits limits,
    DeviceOperationGate operationGate,
    ILoggerFactory loggerFactory) {

    public async ValueTask<RockchipDeviceLease> OpenAsync(
        DeviceTarget target,
        string? loaderBlobId,
        string? loaderUrl,
        JsonRpcCallContext context,
        bool useEmbeddedLoader = false,
        string? firmwarePath = null) {
        var gate = await operationGate.AcquireAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        var logger = loggerFactory.CreateLogger<DeviceAccess>();
        try {
            if (!string.IsNullOrWhiteSpace(loaderBlobId) ||
                !string.IsNullOrWhiteSpace(loaderUrl)) {
                await DownloadLoaderAsync(
                        target,
                        loaderBlobId,
                        loaderUrl,
                        logger,
                        context)
                    .ConfigureAwait(false);
            } else if (useEmbeddedLoader &&
                firmwarePath is not null) {
                var mode = await DeviceConnector.GetModeAsync(
                        target,
                        loggerFactory,
                        context.CancellationToken)
                    .ConfigureAwait(false);
                if (mode == RockchipUsbMode.MaskRom) {
                    logger.LogInformation(
                        "MaskROM detected; using the update image loader");
                    await DownloadLoaderAsync(
                            target,
                            loaderBlobId: null,
                            loaderUrl: firmwarePath,
                            logger,
                            context)
                        .ConfigureAwait(false);
                }
            }

            var session = await DeviceConnector.OpenAsync(
                    target,
                    loggerFactory,
                    logger,
                    context.CancellationToken)
                .ConfigureAwait(false);
            var device = new RockchipDevice(
                session.Transport,
                loggerFactory: loggerFactory);
            return new RockchipDeviceLease(
                device,
                session,
                gate);
        } catch {
            gate.Dispose();
            throw;
        }
    }

    private async Task DownloadLoaderAsync(
        DeviceTarget target,
        string? loaderBlobId,
        string? loaderUrl,
        ILogger logger,
        JsonRpcCallContext context) {
        var location = !string.IsNullOrWhiteSpace(loaderBlobId)
            ? blobs.Get(loaderBlobId).Path
            : loaderUrl;
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The loader input is missing.");
        }

        await using var input = await InputFile.OpenAsync(
                location,
                logger,
                context.StatusAsync,
                (current, total) => context.ProgressAsync(
                    "loader",
                    current,
                    total),
                context.CancellationToken,
                limits: limits)
            .ConfigureAwait(false);
        var session = await DeviceConnector.OpenAsync(
                target,
                loggerFactory,
                logger,
                context.CancellationToken)
            .ConfigureAwait(false);
        await using (session) {
            await using var device = new RockchipDevice(
                session.Transport,
                loggerFactory: loggerFactory);
            await device.DownloadBootAsync(
                    input.Path,
                    context.CancellationToken)
                .ConfigureAwait(false);
        }

        logger.LogInformation("Loader prepared for command execution");
        await Task.Delay(
                TimeSpan.FromSeconds(1),
                context.CancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class RockchipDeviceLease(
    RockchipDevice device,
    DeviceSession session,
    IDisposable operationGate) : IAsyncDisposable {

    public RockchipDevice Device { get; } = device;

    public async ValueTask DisposeAsync() {
        try {
            await Device.DisposeAsync().ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
        } finally {
            operationGate.Dispose();
        }
    }
}
