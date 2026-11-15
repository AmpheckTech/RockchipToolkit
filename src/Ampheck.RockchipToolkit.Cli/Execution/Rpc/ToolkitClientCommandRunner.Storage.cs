using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static partial class ToolkitClientCommandRunner {
    private static async Task<int> RunFlashAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var firmware = await payload.PrepareFirmwareAsync(
                options.Source!,
                options.FetchLocation,
                CancellationToken.None)
            .ConfigureAwait(false);
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        await client.FlashFirmwareAsync(
                new FirmwareFlashParams {
                    Target = target,
                    FirmwareBlobId = firmware.BlobId,
                    FirmwareUrl = firmware.Url,
                    FirmwareServerPath = firmware.ServerPath,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    ResetAfterFlash = options.ResetAfterFlash
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine("Flash complete");
        return 0;
    }

    private static async Task<int> RunReadAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        ILogger logger,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        var outputServerPath =
            client.UseServerPaths &&
            !IsHttpLocation(options.File!)
                ? Path.GetFullPath(options.File!)
                : null;
        var file = await client.ReadStorageAsync(
                new StorageReadParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    OutputServerPath = outputServerPath,
                    Sector = options.Sector ?? 0,
                    Count = options.Count ?? 1
                },
                report)
            .ConfigureAwait(false);
        try {
            await BlobOutput.WriteAsync(
                    client,
                    file,
                    options.File!,
                    console,
                    logger)
                .ConfigureAwait(false);
        } finally {
            if (file.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId)
                    .ConfigureAwait(false);
            }
        }

        Console.WriteLine($"Read {file.Length:N0} bytes");
        return 0;
    }

    private static async Task<int> RunWriteAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        ILogger logger,
        ConsoleRenderer console,
        Action<TaskEventNotification> report) {
        var input = await payload.PrepareFileAsync(
                options.File!,
                options.FetchLocation,
                CancellationToken.None)
            .ConfigureAwait(false);
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        var result = await client.WriteStorageAsync(
                new StorageWriteParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    BlobId = input.BlobId,
                    SourceUrl = input.Url,
                    ServerPath = input.ServerPath,
                    Sector = options.Sector ?? 0,
                    Length = options.WriteLength,
                    SourceOffset = options.WriteSourceOffset,
                    EraseBeforeWrite = options.EraseBeforeWrite,
                    Backup = options.Backup is not null,
                    BackupServerPath =
                        client.UseServerPaths &&
                        options.Backup is not null
                            ? Path.GetFullPath(options.Backup)
                            : null
                },
                report)
            .ConfigureAwait(false);
        if (result.Backup is { } backup) {
            try {
                await BlobOutput.WriteAsync(
                        client,
                        backup,
                        options.Backup!,
                        console,
                        logger)
                    .ConfigureAwait(false);
            } finally {
                if (backup.BlobId is { } blobId) {
                    await client.DeleteBlobAsync(blobId)
                        .ConfigureAwait(false);
                }
            }
        }

        Console.WriteLine(
            $"Write complete: {result.BytesWritten:N0} bytes");
        return 0;
    }

    private static async Task<int> RunEraseAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        await client.EraseStorageAsync(
                new StorageEraseParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    Sector = options.Sector ?? 0,
                    Count = options.Count ?? 1
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine("Erase complete");
        return 0;
    }

    private static async Task<int> RunStorageAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        await client.SelectStorageAsync(
                new StorageSelectParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    Storage = options.Storage ??
                        throw new ArgumentException(
                            "A storage type is required.")
                })
            .ConfigureAwait(false);
        Console.WriteLine(
            $"Storage changed to {options.Storage}");
        return 0;
    }

    private static async Task<int> RunResetAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        await client.ResetDeviceAsync(
                new DeviceResetParams {
                    Target = target,
                    Mode = options.Reset
                })
            .ConfigureAwait(false);
        Console.WriteLine("Reset requested");
        return 0;
    }

    private static bool IsHttpLocation(string value) {
        return Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri) &&
            uri.Scheme is "http" or "https";
    }
}
