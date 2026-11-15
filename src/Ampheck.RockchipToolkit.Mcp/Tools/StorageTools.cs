using System.ComponentModel;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

[McpServerToolType]
public sealed class StorageTools(ToolkitRpcClient client) {
    [McpServerTool(
        Name = "flash_firmware",
        Destructive = true,
        OpenWorld = true)]
    [Description(
        "Flash an update image, firmware directory, or ZIP file.")]
    public async Task<TextResult> FlashFirmware(
        [Description("Local update image or ZIP path.")]
        string? firmwarePath = null,
        [Description("HTTP(S) update image or ZIP URL.")]
        string? firmwareUrl = null,
        [Description("Optional local loader path.")]
        string? loaderPath = null,
        [Description("Optional HTTP(S) loader URL.")]
        string? loaderUrl = null,
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        bool resetAfterFlash = true,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        ToolSupport.RequireOne(
            firmwarePath,
            firmwareUrl,
            "Firmware input");
        if (!string.IsNullOrWhiteSpace(loaderPath) ||
            !string.IsNullOrWhiteSpace(loaderUrl)) {
            ToolSupport.RequireOne(
                loaderPath,
                loaderUrl,
                "Loader input");
        }

        InputFileReference? firmware = null;
        InputFileReference? loader = null;
        try {
            if (firmwarePath is not null) {
                firmware = await ToolSupport.PrepareInputAsync(
                    client,
                    firmwarePath,
                    progress,
                    cancellationToken,
                    allowDirectory: true);
            }

            if (loaderPath is not null) {
                loader = await ToolSupport.PrepareInputAsync(
                    client,
                    loaderPath,
                    progress,
                    cancellationToken);
            }

            await client.FlashFirmwareAsync(
                new FirmwareFlashParams {
                    Target = ToolSupport.CreateTarget(
                            transport,
                            vendorId,
                            productId),
                    FirmwareBlobId = firmware?.BlobId,
                    FirmwareUrl = firmwareUrl,
                    FirmwareServerPath = firmware?.ServerPath,
                    LoaderBlobId = loader?.BlobId,
                    LoaderUrl = loaderUrl,
                    LoaderServerPath = loader?.ServerPath,
                    ResetAfterFlash = resetAfterFlash
                },
                new ToolProgress(progress).Report,
                cancellationToken);
            return new TextResult {
                Text = "Flash complete."
            };
        } finally {
            if (firmware?.BlobId is { } firmwareBlobId) {
                await client.DeleteBlobAsync(firmwareBlobId);
            }

            if (loader?.BlobId is { } loaderBlobId) {
                await client.DeleteBlobAsync(loaderBlobId);
            }
        }
    }

    [McpServerTool(
        Name = "read_storage",
        UseStructuredContent = true)]
    [Description("Read sectors from device storage into a local file.")]
    public async Task<FileReference> ReadStorage(
        [Description("Local destination file or directory.")]
        string destinationPath,
        [Description("Starting sector.")]
        uint sector = 0,
        [Description("Number of 512-byte sectors.")]
        ushort count = 1,
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        var file = await client.ReadStorageAsync(
            new StorageReadParams {
                Target = ToolSupport.CreateTarget(
                        transport,
                        vendorId,
                        productId),
                Sector = sector,
                Count = count,
                OutputServerPath = client.UseServerPaths
                        ? Path.GetFullPath(destinationPath)
                        : null
            },
            new ToolProgress(progress).Report,
            cancellationToken);
        try {
            return await ToolSupport.SaveOutputAsync(
                client,
                file,
                destinationPath,
                progress,
                cancellationToken);
        } finally {
            if (file.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId);
            }
        }
    }

    [McpServerTool(
        Name = "write_storage",
        Destructive = true,
        UseStructuredContent = true)]
    [Description("Write a local file to device storage sectors.")]
    public async Task<StorageWriteToolResult> WriteStorage(
        [Description("Local source file.")]
        string sourcePath,
        [Description("Starting sector.")]
        uint sector,
        [Description("Optional byte length.")]
        long? length = null,
        [Description("Optional source file offset.")]
        long? sourceOffset = null,
        [Description("Erase target sectors before writing.")]
        bool eraseBeforeWrite = true,
        [Description("Optional local backup destination.")]
        string? backupPath = null,
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        var input = await ToolSupport.PrepareInputAsync(
            client,
            sourcePath,
            progress,
            cancellationToken);
        try {
            var result = await client.WriteStorageAsync(
                new StorageWriteParams {
                    Target = ToolSupport.CreateTarget(
                            transport,
                            vendorId,
                            productId),
                    BlobId = input.BlobId,
                    ServerPath = input.ServerPath,
                    Sector = sector,
                    Length = length,
                    SourceOffset = sourceOffset,
                    EraseBeforeWrite = eraseBeforeWrite,
                    Backup = backupPath is not null,
                    BackupServerPath =
                            client.UseServerPaths &&
                            backupPath is not null
                                ? Path.GetFullPath(backupPath)
                                : null
                },
                new ToolProgress(progress).Report,
                cancellationToken);
            string? savedBackup = null;
            if (result.Backup is { } backup &&
                backupPath is not null) {
                try {
                    var saved = await ToolSupport.SaveOutputAsync(
                        client,
                        backup,
                        backupPath,
                        progress,
                        cancellationToken);
                    savedBackup = saved.Path;
                } finally {
                    if (backup.BlobId is { } backupBlobId) {
                        await client.DeleteBlobAsync(backupBlobId);
                    }
                }
            }

            return new StorageWriteToolResult {
                BytesWritten = result.BytesWritten,
                BackupPath = savedBackup
            };
        } finally {
            if (input.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId);
            }
        }
    }

    [McpServerTool(
        Name = "erase_storage",
        Destructive = true)]
    [Description("Erase device storage sectors.")]
    public async Task<TextResult> EraseStorage(
        [Description("Starting sector.")]
        uint sector,
        [Description("Number of 512-byte sectors.")]
        ushort count,
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        await client.EraseStorageAsync(
            new StorageEraseParams {
                Target = ToolSupport.CreateTarget(
                    transport,
                    vendorId,
                    productId),
                Sector = sector,
                Count = count
            },
            new ToolProgress(progress).Report,
            cancellationToken);
        return new TextResult { Text = "Erase complete." };
    }

    [McpServerTool(Name = "select_storage")]
    [Description("Select eMMC, SD card, or SPI NOR storage.")]
    public async Task<TextResult> SelectStorage(
        [Description("emmc, sd-card, or spi-nor.")]
        string storage,
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        await client.SelectStorageAsync(
            new StorageSelectParams {
                Target = ToolSupport.CreateTarget(
                    transport,
                    vendorId,
                    productId),
                Storage = ParseStorage(storage)
            },
            cancellationToken);
        return new TextResult {
            Text = $"Storage selected: {storage}."
        };
    }

    private static ToolkitStorage ParseStorage(string value) {
        return value.ToLowerInvariant() switch {
            "emmc" => ToolkitStorage.Emmc,
            "sd-card" => ToolkitStorage.SdCard,
            "spi-nor" => ToolkitStorage.SpiNor,
            _ => throw new ArgumentException(
                "storage must be emmc, sd-card, or spi-nor.",
                nameof(value))
        };
    }

}
