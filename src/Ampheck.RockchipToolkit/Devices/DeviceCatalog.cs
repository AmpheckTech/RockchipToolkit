using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Protocol.RockUsb;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Devices;

public sealed class DeviceCatalog {
    private readonly DeviceAccess _access;
    private readonly DeviceConnector _connector;

    public DeviceCatalog(IUsbDeviceProviderFactory hardware) {
        ArgumentNullException.ThrowIfNull(hardware);
        _connector = new DeviceConnector(hardware);
        _access = new DeviceAccess(
            new RockchipBootLoader(),
            _connector);
    }

    public async ValueTask<IReadOnlyList<DeviceDescriptor>> ListDevicesAsync(
        DeviceTarget target,
        CancellationToken cancellationToken
    ) {
        var devices = await _connector.ListAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
        return [.. devices.Select(device => {
            var identity = RockchipUsbCatalog.Resolve(
                device.VendorId,
                device.ProductId);
            return new DeviceDescriptor {
                VendorId = device.VendorId,
                ProductId = device.ProductId,
                UsbVersion = device.UsbVersion,
                Mode = device.Mode,
                SerialNumber = device.SerialNumber,
                PlatformId = device.PlatformId,
                Description = identity is null
                    ? null
                    : $"{identity.Family} {identity.Description}"
            };
        })];
    }

    public async ValueTask<DeviceInfo> GetInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
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
        return new DeviceInfo(chip, flash);
    }

    public async ValueTask<RockchipChipInfo> GetChipInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        return RockchipChipInfoParser.Parse(
            await lease.Device.GetChipInfoAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    public async ValueTask<string> GetChipIdAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToHexString(
            await lease.Device.GetFlashIdAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    public async ValueTask<RockchipCapabilityFlags> GetCapabilityAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
                target,
                loaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        return RockchipCapabilityParser.Parse(
            await lease.Device.GetCapabilityAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    public async ValueTask DownloadLoaderAsync(
        DeviceTarget target,
        string? location,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
                target,
                location,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask ResetAsync(
        DeviceTarget target,
        ResetMode mode,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
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
