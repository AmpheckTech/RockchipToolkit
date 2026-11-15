using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;
using Ampheck.RockchipToolkit.Server.Devices;
using Ampheck.RockchipToolkit.Server.Rpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Firmware;

[JsonRpcService]
internal sealed class FirmwareService(
    DeviceAccess access,
    BlobStore blobs,
    ToolkitTransferLimits limits,
    ILoggerFactory loggerFactory) {

    [JsonRpcMethod(JsonRpcMethods.FirmwareFlash, longRunning: true)]
    public async ValueTask<EmptyResult> FlashAsync(
        FirmwareFlashParams parameters,
        JsonRpcCallContext context) {
        var location = parameters.FirmwareServerPath is not null
            ? RpcFileAccess.RequireServerPath(
                context,
                parameters.FirmwareServerPath,
                "Firmware")
            : !string.IsNullOrWhiteSpace(parameters.FirmwareBlobId)
                ? blobs.Get(parameters.FirmwareBlobId).Path
                : parameters.FirmwareUrl;
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The flash request contains no firmware source.");
        }

        var logger = loggerFactory.CreateLogger<FirmwareService>();
        ValueTask ReportFirmwareProgress(
            long current,
            long total) =>
            context.ProgressAsync(
                "firmware",
                current,
                total);
        ValueTask ReportEraseProgress(
            long current,
            long total) =>
            context.ProgressAsync(
                "erase",
                current,
                total);
        var files = new FirmwareFileContext(
            logger,
            context.StatusAsync,
            ReportFirmwareProgress,
            limits);
        await using var input = await files.OpenFlashInputAsync(
                location,
                context.CancellationToken)
            .ConfigureAwait(false);
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context,
                useEmbeddedLoader:
                    input.SourceKind == FlashSourceKind.UpdateImage,
                firmwarePath: input.Location)
            .ConfigureAwait(false);
        var flashContext = new FirmwareFlashContext(
            lease.Device,
            files,
            ReportEraseProgress);
        if (input.SourceKind == FlashSourceKind.UpdateImage) {
            await new UpdateImageFlasher(flashContext).FlashAsync(
                    input.Location,
                    context.CancellationToken)
                .ConfigureAwait(false);
        } else {
            await new DirectoryFlasher(flashContext).FlashAsync(
                    input.Location,
                    context.CancellationToken)
                .ConfigureAwait(false);
        }

        if (parameters.ResetAfterFlash) {
            await context.StatusAsync("Resetting device")
                .ConfigureAwait(false);
            await lease.Device.ResetAsync(ResetMode.Normal)
                .ConfigureAwait(false);
        }

        return new EmptyResult();
    }
}
