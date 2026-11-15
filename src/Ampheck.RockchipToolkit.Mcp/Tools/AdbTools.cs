using System.ComponentModel;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

[McpServerToolType]
public sealed class AdbTools(ToolkitRpcClient client) {
    [McpServerTool(
        Name = "list_adb_devices",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("List devices known to the official ADB server.")]
    public async Task<AdbDevicesResult> ListDevices(
        [Description("Optional ADB serial number.")]
        string? serialNumber = null,
        CancellationToken cancellationToken = default) {
        return await client.RequestAsync<
            AdbDevicesParams,
            AdbDevicesResult>(
                JsonRpcMethods.AdbDevices,
                new AdbDevicesParams {
                    SerialNumber = serialNumber
                },
                cancellationToken: cancellationToken);
    }

    [McpServerTool(Name = "adb_shell")]
    [Description("Run a non-interactive command through ADB shell.")]
    public async Task<TextResult> Shell(
        [Description("Shell command to execute.")]
        string command,
        string? serialNumber = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        return await client.RequestAsync<AdbShellParams, TextResult>(
            JsonRpcMethods.AdbShell,
            new AdbShellParams {
                SerialNumber = serialNumber,
                Command = command
            },
            new ToolProgress(progress).Report,
            cancellationToken);
    }

    [McpServerTool(Name = "adb_push")]
    [Description("Copy a local file to an ADB device.")]
    public async Task<TextResult> Push(
        [Description("Local source file.")]
        string localPath,
        [Description("Remote destination path.")]
        string destination,
        string? serialNumber = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        var input = await ToolSupport.PrepareInputAsync(
            client,
            localPath,
            progress,
            cancellationToken);
        try {
            await client.RequestAsync<AdbPushParams, EmptyResult>(
                JsonRpcMethods.AdbPush,
                new AdbPushParams {
                    SerialNumber = serialNumber,
                    BlobId = input.BlobId,
                    ServerPath = input.ServerPath,
                    Destination = destination
                },
                new ToolProgress(progress).Report,
                cancellationToken);
            return new TextResult {
                Text = $"Pushed {localPath} to {destination}."
            };
        } finally {
            if (input.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId);
            }
        }
    }

    [McpServerTool(
        Name = "adb_pull",
        UseStructuredContent = true)]
    [Description("Copy a file from an ADB device to the local machine.")]
    public async Task<FileReference> Pull(
        [Description("Remote source path.")]
        string remotePath,
        [Description("Local destination file or directory.")]
        string destinationPath,
        string? serialNumber = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        var file = await client.RequestAsync<
            AdbPullParams,
            FileReference>(
                JsonRpcMethods.AdbPull,
                new AdbPullParams {
                    SerialNumber = serialNumber,
                    Source = remotePath,
                    DestinationServerPath = client.UseServerPaths
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

    [McpServerTool(Name = "reboot_adb_device")]
    [Description("Reboot an ADB device.")]
    public async Task<TextResult> Reboot(
        [Description("normal, recovery, or bootloader.")]
        string mode = "normal",
        string? serialNumber = null,
        CancellationToken cancellationToken = default) {
        await client.RequestAsync<AdbRebootParams, EmptyResult>(
            JsonRpcMethods.AdbReboot,
            new AdbRebootParams {
                SerialNumber = serialNumber,
                Mode = mode.ToLowerInvariant() switch {
                    "normal" => ToolkitAdbRebootMode.System,
                    "recovery" => ToolkitAdbRebootMode.Recovery,
                    "bootloader" => ToolkitAdbRebootMode.Bootloader,
                    _ => throw new ArgumentException(
                        "mode must be normal, recovery, or bootloader.",
                        nameof(mode))
                }
            },
            cancellationToken: cancellationToken);
        return new TextResult {
            Text = $"ADB reboot requested: {mode}."
        };
    }
}
