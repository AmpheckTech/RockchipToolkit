using System.ComponentModel;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

[McpServerToolType]
public sealed class DeviceTools(ToolkitRpcClient client) {
    [McpServerTool(
        Name = "list_devices",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("List connected RockUSB devices.")]
    public async Task<DeviceListResult> ListDevices(
        [Description("Transport: auto, rockusb, or libusb.")]
        string transport = "auto",
        [Description("Optional USB vendor ID.")]
        ushort? vendorId = null,
        [Description("Optional USB product ID.")]
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        return await client.ListDevicesAsync(
            new DeviceListParams {
                Target = ToolSupport.CreateTarget(
                        transport,
                        vendorId,
                        productId)
            },
            cancellationToken: cancellationToken);
    }

    [McpServerTool(
        Name = "get_device_info",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("Read RockUSB chip and flash information.")]
    public async Task<DeviceInfoResult> GetDeviceInfo(
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        return await client.GetDeviceInfoAsync(
            new DeviceInfoParams {
                Target = ToolSupport.CreateTarget(
                        transport,
                        vendorId,
                        productId)
            },
            cancellationToken: cancellationToken);
    }

    [McpServerTool(
        Name = "get_chip_info",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("Read chip identifier and platform.")]
    public async Task<DeviceChipInfoResult> GetChipInfo(
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        return await client.GetChipInfoAsync(
            new DeviceChipInfoParams {
                Target = ToolSupport.CreateTarget(
                        transport,
                        vendorId,
                        productId)
            },
            cancellationToken: cancellationToken);
    }

    [McpServerTool(
        Name = "get_chip_id",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("Read the device chip ID.")]
    public async Task<DeviceChipIdResult> GetChipId(
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        return await client.GetChipIdAsync(
            new DeviceChipIdParams {
                Target = ToolSupport.CreateTarget(
                        transport,
                        vendorId,
                        productId)
            },
            cancellationToken: cancellationToken);
    }

    [McpServerTool(
        Name = "get_capabilities",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("Read RockUSB capability flags.")]
    public async Task<DeviceCapabilityResult> GetCapabilities(
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        return await client.GetCapabilitiesAsync(
            new DeviceCapabilityParams {
                Target = ToolSupport.CreateTarget(
                        transport,
                        vendorId,
                        productId)
            },
            cancellationToken: cancellationToken);
    }

    [McpServerTool(Name = "load_loader")]
    [Description("Download a loader to a device in MaskROM or Loader mode.")]
    public async Task<TextResult> LoadLoader(
        [Description("Local loader file path.")]
        string? path = null,
        [Description("HTTP(S) loader URL.")]
        string? url = null,
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        ToolSupport.RequireOne(path, url, "Loader input");
        InputFileReference? input = null;
        try {
            if (path is not null) {
                input = await ToolSupport.PrepareInputAsync(
                    client,
                    path,
                    progress,
                    cancellationToken);
            }

            await client.LoadLoaderAsync(
                new DeviceLoaderParams {
                    Target = ToolSupport.CreateTarget(
                            transport,
                            vendorId,
                            productId),
                    BlobId = input?.BlobId,
                    Url = url,
                    ServerPath = input?.ServerPath
                },
                new ToolProgress(progress).Report,
                cancellationToken);
            return new TextResult {
                Text = "Loader downloaded."
            };
        } finally {
            if (input?.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId);
            }
        }
    }

    [McpServerTool(Name = "reset_device")]
    [Description("Reset the device into normal, recovery, or another mode.")]
    public async Task<TextResult> ResetDevice(
        [Description("normal, reset-mass-storage, power-off, reset-mask-rom, or disconnect.")]
        string mode = "normal",
        string transport = "auto",
        ushort? vendorId = null,
        ushort? productId = null,
        CancellationToken cancellationToken = default) {
        await client.ResetDeviceAsync(
            new DeviceResetParams {
                Target = ToolSupport.CreateTarget(
                    transport,
                    vendorId,
                    productId),
                Mode = ParseResetMode(mode)
            },
            cancellationToken);
        return new TextResult { Text = $"Reset requested: {mode}." };
    }

    private static ToolkitResetMode ParseResetMode(string value) {
        return value.ToLowerInvariant() switch {
            "normal" => ToolkitResetMode.Normal,
            "reset-mass-storage" => ToolkitResetMode.ResetMassStorage,
            "power-off" => ToolkitResetMode.PowerOff,
            "reset-mask-rom" => ToolkitResetMode.ResetMaskRom,
            "disconnect" => ToolkitResetMode.Disconnect,
            _ => throw new ArgumentException(
                "Unknown reset mode.",
                nameof(value))
        };
    }

}
