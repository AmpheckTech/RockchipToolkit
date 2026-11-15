using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class StorageCommandRunner(CliCommandContext context) {
    public async Task<int> RunFlashAsync(
        FirmwareFlashArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var firmware = await session.Payload.PrepareFirmwareAsync(
                options.Source,
                options.FetchLocation,
                cancellationToken)
            .ConfigureAwait(false);
        var loader = string.IsNullOrWhiteSpace(options.Loader)
            ? new PayloadReference(
                BlobId: null,
                Url: null,
                ServerPath: null)
            : await session.Payload.PrepareFileAsync(
                    options.Loader,
                    options.FetchLocation,
                    cancellationToken)
                .ConfigureAwait(false);
        await session.Client.FlashFirmwareAsync(
                new FirmwareFlashParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    FirmwareBlobId = firmware.BlobId,
                    FirmwareUrl = firmware.Url,
                    FirmwareServerPath = firmware.ServerPath,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    ResetAfterFlash = options.ResetAfterFlash
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("Flash complete");
        return 0;
    }

    public async Task<int> RunReadAsync(
        StorageReadArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        var outputServerPath =
            session.Client.UseServerPaths &&
            !IsHttpLocation(options.Output)
                ? Path.GetFullPath(options.Output)
                : null;
        var file = await session.Client.ReadStorageAsync(
                new StorageReadParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    OutputServerPath = outputServerPath,
                    Sector = options.Sector,
                    Count = options.Count
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        try {
            await BlobOutput.WriteAsync(
                    session.Client,
                    file,
                    options.Output,
                    context.Console,
                    context.Logger,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            if (file.BlobId is { } blobId) {
                await session.Client.DeleteBlobAsync(blobId)
                    .ConfigureAwait(false);
            }
        }

        Console.WriteLine($"Read {file.Length:N0} bytes");
        return 0;
    }

    public async Task<int> RunWriteAsync(
        StorageWriteArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var input = await session.Payload.PrepareFileAsync(
                options.Input,
                options.FetchLocation,
                cancellationToken)
            .ConfigureAwait(false);
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        var result = await session.Client.WriteStorageAsync(
                new StorageWriteParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    BlobId = input.BlobId,
                    SourceUrl = input.Url,
                    ServerPath = input.ServerPath,
                    Sector = options.Sector,
                    Length = options.Length,
                    SourceOffset = options.SourceOffset,
                    EraseBeforeWrite = options.EraseBeforeWrite,
                    Backup = options.Backup is not null,
                    BackupServerPath =
                        session.Client.UseServerPaths &&
                        options.Backup is not null
                            ? Path.GetFullPath(options.Backup)
                            : null
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Backup is { } backup) {
            try {
                await BlobOutput.WriteAsync(
                        session.Client,
                        backup,
                        options.Backup!,
                        context.Console,
                        context.Logger,
                        cancellationToken)
                    .ConfigureAwait(false);
            } finally {
                if (backup.BlobId is { } blobId) {
                    await session.Client.DeleteBlobAsync(blobId)
                        .ConfigureAwait(false);
                }
            }
        }

        Console.WriteLine(
            $"Write complete: {result.BytesWritten:N0} bytes");
        return 0;
    }

    public async Task<int> RunEraseAsync(
        StorageEraseArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        await session.Client.EraseStorageAsync(
                new StorageEraseParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    Sector = options.Sector,
                    Count = options.Count
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("Erase complete");
        return 0;
    }

    public async Task<int> RunStorageAsync(
        StorageSelectArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        await session.Client.SelectStorageAsync(
                new StorageSelectParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath,
                    Storage = options.Storage
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"Storage changed to {options.Storage}");
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
