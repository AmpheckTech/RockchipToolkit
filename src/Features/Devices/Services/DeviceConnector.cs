using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.HardwareTransport.Backends;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Devices.Services;

public sealed class DeviceConnector(UsbDeviceProviderFactory providerFactory) {
    internal async Task<IReadOnlyList<UsbDeviceInfo>>
        ListAsync(
            DeviceTarget target,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) {
        await using var provider = providerFactory.Create(
            target.Transport,
            target.VendorId,
            target.ProductId,
            loggerFactory);
        var devices = await provider.EnumerateAsync(cancellationToken)
            .ConfigureAwait(false);
        return devices
            .Where(device =>
                device.Mode != RockchipUsbMode.None &&
                (!target.VendorId.HasValue ||
                    device.VendorId == target.VendorId) &&
                (!target.ProductId.HasValue ||
                    device.ProductId == target.ProductId))
            .ToArray();
    }

    internal async Task<(IUsbTransport Transport, IUsbDeviceProvider Provider)> OpenAsync(
        DeviceTarget target,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken cancellationToken
    ) {
        var provider = providerFactory.Create(
            target.Transport,
            target.VendorId,
            target.ProductId,
            loggerFactory);
        try {
            var devices = await provider.EnumerateAsync(cancellationToken)
                .ConfigureAwait(false);
            logger.LogDebug(
                "Enumerated {DeviceCount} RockUSB device(s)",
                devices.Count);
            var selected = SelectDevice(
                devices,
                target.VendorId,
                target.ProductId);
            if (selected is null) {
                throw new InvalidOperationException(
                    "No matching RockUSB device was found. " +
                    "Make sure the board is in MaskROM or Loader mode " +
                    "and uses a supported RockUSB driver.");
            }

            logger.LogInformation(
                "Opening device {VendorId:X4}:{ProductId:X4} in {Mode} mode",
                selected.VendorId,
                selected.ProductId,
                selected.Mode);
            return (
                await provider.OpenAsync(selected, cancellationToken)
                    .ConfigureAwait(false),
                provider);
        } catch (Exception error) when (
            IsPermissionFailure(error)) {
            await provider.DisposeAsync().ConfigureAwait(false);
            var hint = OperatingSystem.IsWindows()
                ? "Install the Rockchip USB driver and reconnect " +
                  "the device."
                : "Configure a udev rule, join the dialout group, " +
                  "or run the command with sudo.";
            throw new InvalidOperationException(
                $"{error.Message} {hint}",
                error);
        } catch {
            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal async Task<RockchipUsbMode?> GetModeAsync(
        DeviceTarget target,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    ) {
        return await GetModeAsync(
                target.Transport,
                target.VendorId,
                target.ProductId,
                loggerFactory,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<RockchipUsbMode?> GetModeAsync(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken
    ) {
        await using var provider = providerFactory.Create(
            transport,
            vendorId,
            productId,
            loggerFactory);
        var devices = await provider.EnumerateAsync(cancellationToken)
            .ConfigureAwait(false);
        return SelectDevice(
            devices,
            vendorId,
            productId)?.Mode;
    }

    private static UsbDeviceInfo? SelectDevice(
        IReadOnlyList<UsbDeviceInfo> devices,
        ushort? vendorId,
        ushort? productId
    ) {
        var matching = devices
            .Where(device =>
                device.Mode != RockchipUsbMode.None &&
                (!vendorId.HasValue ||
                    device.VendorId == vendorId) &&
                (!productId.HasValue ||
                    device.ProductId == productId))
            .ToArray();
        if (matching.Length > 1) {
            throw new InvalidOperationException(
                "Multiple matching RockUSB devices were found. " +
                "Use --vid and --pid to select one.");
        }

        return matching.SingleOrDefault();
    }

    private static bool IsPermissionFailure(Exception error) {
        for (var current = error;
            current is not null;
            current = current.InnerException) {
            if (current is UnauthorizedAccessException ||
                current is System.ComponentModel.Win32Exception {
                    NativeErrorCode: 5 or 13
                } ||
                current.Message.Contains(
                    "insufficient permissions",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "permission denied",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "LIBUSB_ERROR_ACCESS",
                    StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
