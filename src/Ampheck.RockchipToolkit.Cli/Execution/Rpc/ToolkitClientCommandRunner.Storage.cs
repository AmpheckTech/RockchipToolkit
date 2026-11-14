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
        await client.RequestAsync<
            FirmwareFlashParams,
            EmptyResult>(
                JsonRpcMethods.FirmwareFlash,
                new FirmwareFlashParams {
                    Target = target,
                    FirmwareBlobId = firmware.BlobId,
                    FirmwareUrl = firmware.Url,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
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
        var blob = await client.RequestAsync<
            StorageReadParams,
            BlobReference>(
                JsonRpcMethods.StorageRead,
                new StorageReadParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    Sector = options.Sector ?? 0,
                    Count = options.Count ?? 1
                },
                report)
            .ConfigureAwait(false);
        try {
            await BlobOutput.WriteAsync(
                    client,
                    blob,
                    options.File!,
                    console,
                    logger)
                .ConfigureAwait(false);
        } finally {
            await client.DeleteBlobAsync(blob.BlobId)
                .ConfigureAwait(false);
        }

        Console.WriteLine($"Read {blob.Length:N0} bytes");
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
        var result = await client.RequestAsync<
            StorageWriteParams,
            StorageWriteResult>(
                JsonRpcMethods.StorageWrite,
                new StorageWriteParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    BlobId = input.BlobId,
                    SourceUrl = input.Url,
                    Sector = options.Sector ?? 0,
                    Length = options.WriteLength,
                    SourceOffset = options.WriteSourceOffset,
                    EraseBeforeWrite = options.EraseBeforeWrite,
                    Backup = options.Backup is not null
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
                await client.DeleteBlobAsync(backup.BlobId)
                    .ConfigureAwait(false);
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
        await client.RequestAsync<
            StorageEraseParams,
            EmptyResult>(
                JsonRpcMethods.StorageErase,
                new StorageEraseParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
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
        await client.RequestAsync<
            StorageSelectParams,
            EmptyResult>(
                JsonRpcMethods.StorageSelect,
                new StorageSelectParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    Storage = options.Storage ??
                        throw new ArgumentException(
                            "A storage type is required.")
                },
                report)
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
        await client.RequestAsync<
            DeviceResetParams,
            EmptyResult>(
                JsonRpcMethods.DeviceReset,
                new DeviceResetParams {
                    Target = target,
                    Mode = options.Reset
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine("Reset requested");
        return 0;
    }
}
