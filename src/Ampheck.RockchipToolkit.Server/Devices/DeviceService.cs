using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Rpc;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Devices;

[JsonRpcService]
internal sealed class DeviceService(
    DeviceAccess access,
    ILoggerFactory loggerFactory) {

    [JsonRpcMethod(JsonRpcMethods.DeviceList)]
    public async ValueTask<DeviceListResult> ListAsync(
        DeviceListParams parameters,
        JsonRpcCallContext context) {
        var devices = await DeviceConnector.ListAsync(
                parameters.Target,
                loggerFactory,
                context.CancellationToken)
            .ConfigureAwait(false);
        return new DeviceListResult {
            Devices = devices.Select(device => {
                var identity = RockchipUsbCatalog.Resolve(
                    device.VendorId,
                    device.ProductId);
                return new DeviceDescriptor {
                    VendorId = device.VendorId,
                    ProductId = device.ProductId,
                    UsbVersion = device.UsbVersion,
                    Mode = device.Mode.ToString(),
                    SerialNumber = device.SerialNumber,
                    PlatformId = device.PlatformId,
                    Description = identity is null
                        ? null
                        : $"{identity.Family} {identity.Description}"
                };
            }).ToArray()
        };
    }

    [JsonRpcMethod(JsonRpcMethods.DeviceInfo)]
    public async ValueTask<DeviceInfoResult> InfoAsync(
        DeviceInfoParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        var chip = RockchipChipInfoParser.Parse(
            await lease.Device.GetChipInfoAsync(
                    context.CancellationToken)
                .ConfigureAwait(false));
        var flash = RockchipFlashInfoParser.Parse(
            await lease.Device.GetFlashInfoAsync(
                    context.CancellationToken)
                .ConfigureAwait(false));
        return new DeviceInfoResult {
            ChipIdentifier = chip.Identifier,
            PlatformName = chip.PlatformName,
            Manufacturer = flash.Manufacturer,
            ManufacturerCode = flash.ManufacturerCode,
            CapacityBytes = checked((long)flash.CapacityBytes),
            BlockSizeBytes = flash.BlockSizeBytes,
            PageSizeBytes = flash.PageSizeBytes,
            BlockCount = flash.BlockCount,
            EccBits = flash.EccBits,
            AccessTime = flash.AccessTime,
            ChipSelectMask = flash.ChipSelectMask
        };
    }

    [JsonRpcMethod(JsonRpcMethods.DeviceChipInfo)]
    public async ValueTask<DeviceChipInfoResult> ChipInfoAsync(
        DeviceChipInfoParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        var info = RockchipChipInfoParser.Parse(
            await lease.Device.GetChipInfoAsync(
                    context.CancellationToken)
                .ConfigureAwait(false));
        return new DeviceChipInfoResult {
            Identifier = info.Identifier,
            PlatformName = info.PlatformName
        };
    }

    [JsonRpcMethod(JsonRpcMethods.DeviceChipId)]
    public async ValueTask<DeviceChipIdResult> ChipIdAsync(
        DeviceChipIdParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        return new DeviceChipIdResult {
            ChipId = Convert.ToHexString(
                await lease.Device.GetFlashIdAsync(
                        context.CancellationToken)
                    .ConfigureAwait(false))
        };
    }

    [JsonRpcMethod(JsonRpcMethods.DeviceCapability)]
    public async ValueTask<DeviceCapabilityResult> CapabilityAsync(
        DeviceCapabilityParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.LoaderBlobId,
                    parameters.LoaderUrl,
                    parameters.LoaderServerPath),
                context)
            .ConfigureAwait(false);
        var capability = RockchipCapabilityParser.Parse(
            await lease.Device.GetCapabilityAsync(
                    context.CancellationToken)
                .ConfigureAwait(false));
        return new DeviceCapabilityResult {
            Flags = (ulong)capability.Flags
        };
    }

    [JsonRpcMethod(JsonRpcMethods.DeviceLoader, longRunning: true)]
    public async ValueTask<EmptyResult> LoaderAsync(
        DeviceLoaderParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    parameters.BlobId,
                    parameters.Url,
                    parameters.ServerPath),
                context)
            .ConfigureAwait(false);
        await context.OutputAsync("Bootloader downloaded")
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    [JsonRpcMethod(JsonRpcMethods.DeviceReset)]
    public async ValueTask<EmptyResult> ResetAsync(
        DeviceResetParams parameters,
        JsonRpcCallContext context) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                new DeviceLoaderInput(
                    BlobId: null,
                    Url: null,
                    ServerPath: null),
                context)
            .ConfigureAwait(false);
        await lease.Device.ResetAsync(
                ToResetMode(parameters.Mode),
                context.CancellationToken)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    private static ResetMode ToResetMode(
        ToolkitResetMode? mode) => mode switch {
            ToolkitResetMode.ResetMassStorage =>
                ResetMode.ResetMassStorage,
            ToolkitResetMode.PowerOff => ResetMode.PowerOff,
            ToolkitResetMode.ResetMaskRom =>
                ResetMode.ResetMaskRom,
            ToolkitResetMode.Disconnect => ResetMode.Disconnect,
            _ => ResetMode.Normal
        };
}
