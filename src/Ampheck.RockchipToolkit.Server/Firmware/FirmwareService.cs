using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;
using Ampheck.RockchipToolkit.Server.Devices;
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
        var location = !string.IsNullOrWhiteSpace(
                parameters.FirmwareBlobId)
            ? blobs.Get(parameters.FirmwareBlobId).Path
            : parameters.FirmwareUrl;
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The flash request contains no firmware source.");
        }

        var logger = loggerFactory.CreateLogger<FirmwareService>();
        await using var input = await FlashInput.OpenAsync(
                location,
                logger,
                context.StatusAsync,
                (current, total) => context.ProgressAsync(
                    "firmware",
                    current,
                    total),
                context.CancellationToken,
                limits: limits)
            .ConfigureAwait(false);
        await using var lease = await access.OpenAsync(
                parameters.Target,
                parameters.LoaderBlobId,
                parameters.LoaderUrl,
                context,
                useEmbeddedLoader:
                    input.SourceKind == FlashSourceKind.UpdateImage,
                firmwarePath: input.Location)
            .ConfigureAwait(false);
        if (input.SourceKind == FlashSourceKind.UpdateImage) {
            await UpdateImageFlasher.FlashAsync(
                    lease.Device,
                    input.Location,
                    logger,
                    context.StatusAsync,
                    (current, total) => context.ProgressAsync(
                        "firmware",
                        current,
                        total),
                    (current, total) => context.ProgressAsync(
                        "erase",
                        current,
                        total),
                    limits,
                    context.CancellationToken)
                .ConfigureAwait(false);
        } else {
            await DirectoryFlasher.FlashAsync(
                    lease.Device,
                    input.Location,
                    logger,
                    context.StatusAsync,
                    (current, total) => context.ProgressAsync(
                        "firmware",
                        current,
                        total),
                    (current, total) => context.ProgressAsync(
                        "erase",
                        current,
                        total),
                    limits,
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
