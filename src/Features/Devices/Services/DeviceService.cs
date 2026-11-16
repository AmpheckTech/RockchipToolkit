using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Devices.Services;

internal sealed partial class DeviceService(
    DeviceAccess access,
    DeviceConnector connector,
    ILoggerFactory loggerFactory
) {

    public async ValueTask<DeviceListResult> ListAsync(
        DeviceTarget target,
        CancellationToken cancellationToken
    ) {
        var devices = await connector.ListAsync(
                target,
                loggerFactory,
                cancellationToken)
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

    public async ValueTask<DeviceInfoResult> InfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        var chip = RockchipChipInfoParser.Parse(
            await lease.Device.GetChipInfoAsync(
                    cancellationToken)
                .ConfigureAwait(false));
        var flash = RockchipFlashInfoParser.Parse(
            await lease.Device.GetFlashInfoAsync(
                    cancellationToken)
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

    public async ValueTask<DeviceChipInfoResult> ChipInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        var info = RockchipChipInfoParser.Parse(
            await lease.Device.GetChipInfoAsync(
                    cancellationToken)
                .ConfigureAwait(false));
        return new DeviceChipInfoResult {
            Identifier = info.Identifier,
            PlatformName = info.PlatformName
        };
    }

    public async ValueTask<DeviceChipIdResult> ChipIdAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        return new DeviceChipIdResult {
            ChipId = Convert.ToHexString(
                await lease.Device.GetFlashIdAsync(
                        cancellationToken)
                    .ConfigureAwait(false))
        };
    }

    public async ValueTask<DeviceCapabilityResult> CapabilityAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        var capability = RockchipCapabilityParser.Parse(
            await lease.Device.GetCapabilityAsync(
                    cancellationToken)
                .ConfigureAwait(false));
        return new DeviceCapabilityResult {
            Flags = (ulong)capability.Flags
        };
    }

    public async ValueTask LoaderAsync(
        DeviceTarget target,
        string? location,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                target,
                location,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.ReportOutput("Bootloader downloaded");
    }

    public async ValueTask ResetAsync(
        DeviceTarget target,
        ResetMode mode,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                target,
                loaderLocation: null,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        await lease.Device.ResetAsync(
                mode,
                cancellationToken)
            .ConfigureAwait(false);
    }


}
