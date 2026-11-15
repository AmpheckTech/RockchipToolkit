using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;
using Ampheck.RockchipToolkit.Server.Devices;
using Ampheck.RockchipToolkit.Server.Firmware;
using Ampheck.RockchipToolkit.Server.Rpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Storage;

[JsonRpcService]
internal sealed class StorageService(
    DeviceAccess access,
    BlobStore blobs,
    ToolkitTransferLimits limits,
    ILoggerFactory loggerFactory) {

    [JsonRpcMethod(JsonRpcMethods.StorageRead, longRunning: true)]
    public async ValueTask<FileReference> ReadAsync(
        StorageReadParams parameters,
        JsonRpcCallContext context) {
        if (parameters.Count == 0) {
            throw new InvalidDataException(
                "The read count must be greater than zero.");
        }

        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        await context.StatusAsync(
                $"Reading {parameters.Count:N0} sector(s) at " +
                $"0x{parameters.Sector:X8}")
            .ConfigureAwait(false);
        var data = await lease.Device.ReadAsync(
                parameters.Sector,
                parameters.Count,
                context.CancellationToken)
            .ConfigureAwait(false);
        var fileName = $"read-{parameters.Sector:X8}.bin";
        if (parameters.OutputServerPath is not null) {
            var path = RpcFileAccess.RequireServerPath(
                context,
                parameters.OutputServerPath,
                "Storage output");
            if (Directory.Exists(path)) {
                path = Path.Combine(path, fileName);
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(
                    path,
                    data,
                    context.CancellationToken)
                .ConfigureAwait(false);
            return RpcFileAccess.CreateFileReference(path);
        }

        var blob = await blobs.CreateAsync(
                fileName,
                (destination, cancellationToken) =>
                    destination.WriteAsync(
                            data,
                            cancellationToken)
                        .AsTask(),
                context.CancellationToken)
            .ConfigureAwait(false);
        return RpcFileAccess.CreateFileReference(blob);
    }

    [JsonRpcMethod(JsonRpcMethods.StorageWrite, longRunning: true)]
    public async ValueTask<StorageWriteResult> WriteAsync(
        StorageWriteParams parameters,
        JsonRpcCallContext context) {
        if (string.IsNullOrWhiteSpace(parameters.ServerPath) &&
            string.IsNullOrWhiteSpace(parameters.BlobId) &&
            string.IsNullOrWhiteSpace(parameters.SourceUrl)) {
            throw new InvalidDataException(
                "The write request contains no input.");
        }

        var logger = loggerFactory.CreateLogger<StorageService>();
        InputFile? downloaded = null;
        var path = parameters.ServerPath is not null
            ? RpcFileAccess.RequireServerPath(
                context,
                parameters.ServerPath,
                "Storage input")
            : parameters.BlobId is { } blobId
                ? blobs.Get(blobId).Path
                : null;
        if (path is null) {
            downloaded = await InputFile.OpenAsync(
                    parameters.SourceUrl!,
                    logger,
                    context.StatusAsync,
                    (current, total) => context.ProgressAsync(
                        "input",
                        current,
                        total),
                    context.CancellationToken,
                    limits: limits)
                .ConfigureAwait(false);
            path = downloaded.Path;
        }

        var sourceOffset = parameters.SourceOffset ?? 0;
        var inputLength = new FileInfo(path).Length;
        var length = parameters.Length ??
            checked(inputLength - sourceOffset);
        ValidateWrite(inputLength, sourceOffset, length);
        var backupPath = parameters.BackupServerPath is not null
            ? RpcFileAccess.RequireServerPath(
                context,
                parameters.BackupServerPath,
                "Storage backup")
            : parameters.Backup
                ? Path.Combine(
                    Path.GetTempPath(),
                    $"ampheck-backup-{Guid.NewGuid():N}.bin")
                : null;
        var temporaryBackup = parameters.BackupServerPath is null;
        if (parameters.BackupServerPath is not null) {
            Directory.CreateDirectory(
                Path.GetDirectoryName(backupPath!)!);
        }
        try {
            await using var lease = await access.OpenAsync(
                    parameters.Target,
                        new DeviceLoaderInput(
                            parameters.LoaderBlobId,
                            parameters.LoaderUrl,
                            parameters.LoaderServerPath),
                    context)
                .ConfigureAwait(false);
            await FirmwareWriter.WriteAsync(
                    lease.Device,
                    path,
                    parameters.Sector,
                    backupPath,
                    parameters.EraseBeforeWrite,
                    logger,
                    context.StatusAsync,
                    (current, total) => context.ProgressAsync(
                        "write",
                        current,
                        total),
                    (current, total) => context.ProgressAsync(
                        "erase",
                        current,
                        total),
                    length,
                    sourceOffset,
                    context.CancellationToken)
                .ConfigureAwait(false);
            FileReference? backup = null;
            if (backupPath is not null) {
                if (parameters.BackupServerPath is not null) {
                    backup = RpcFileAccess.CreateFileReference(
                        backupPath);
                } else {
                    var blob = await blobs.CreateAsync(
                            $"backup-{parameters.Sector:X8}.bin",
                            async (destination, cancellationToken) => {
                                await using var source = new FileStream(
                                    backupPath,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.Read,
                                    128 * 1024,
                                    FileOptions.Asynchronous |
                                    FileOptions.SequentialScan);
                                await source.CopyToAsync(
                                        destination,
                                        cancellationToken)
                                    .ConfigureAwait(false);
                            },
                            context.CancellationToken)
                        .ConfigureAwait(false);
                    backup = RpcFileAccess.CreateFileReference(blob);
                }
            }

            return new StorageWriteResult {
                Backup = backup,
                BytesWritten = length
            };
        } finally {
            if (downloaded is not null) {
                await downloaded.DisposeAsync()
                    .ConfigureAwait(false);
            }

            if (temporaryBackup && backupPath is not null) {
                TryDelete(backupPath);
            }
        }
    }

    [JsonRpcMethod(JsonRpcMethods.StorageErase, longRunning: true)]
    public async ValueTask<EmptyResult> EraseAsync(
        StorageEraseParams parameters,
        JsonRpcCallContext context) {
        if (parameters.Count == 0) {
            throw new InvalidDataException(
                "The erase count must be greater than zero.");
        }

        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        await context.StatusAsync(
                $"Erasing {parameters.Count:N0} sectors at " +
                $"0x{parameters.Sector:X8}")
            .ConfigureAwait(false);
        await lease.Device.EraseAsync(
                parameters.Sector,
                parameters.Count,
                (current, total) => context.ProgressAsync(
                    "erase",
                    current,
                    total),
                context.CancellationToken)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    [JsonRpcMethod(JsonRpcMethods.StorageSelect)]
    public async ValueTask<EmptyResult> SelectAsync(
        StorageSelectParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        await lease.Device.ChangeStorageAsync(
                parameters.Storage switch {
                    ToolkitStorage.SdCard => StorageType.SdCard,
                    ToolkitStorage.SpiNor => StorageType.SpiNor,
                    _ => StorageType.Emmc
                },
                context.CancellationToken)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    private static void ValidateWrite(
        long blobLength,
        long sourceOffset,
        long length) {
        if (sourceOffset < 0 ||
            length <= 0 ||
            sourceOffset > blobLength ||
            length > blobLength - sourceOffset) {
            throw new InvalidDataException(
                "The write range is outside the input blob.");
        }
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
