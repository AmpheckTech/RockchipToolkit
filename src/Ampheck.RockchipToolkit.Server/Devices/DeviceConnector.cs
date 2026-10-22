using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Devices;

public static class DeviceConnector {
    public static async Task<int> WaitForLoaderAsync(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId,
        Func<string, ValueTask> report,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default) {
        for (var attempt = 0; attempt < 40; attempt++) {
            var mode = await GetModeAsync(
                    transport,
                    vendorId,
                    productId,
                    loggerFactory,
                    cancellationToken)
                .ConfigureAwait(false);
            if (mode is RockchipUsbMode.MaskRom or
                RockchipUsbMode.Loader) {
                await report($"loader mode: {mode}")
                    .ConfigureAwait(false);
                await Task.Delay(
                        TimeSpan.FromMilliseconds(500),
                        cancellationToken)
                    .ConfigureAwait(false);
                return 0;
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The device did not enter Loader mode within 20 seconds.");
    }

    internal static async Task<IReadOnlyList<UsbDeviceInfo>>
        ListAsync(
            ToolkitRequest request,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) {
        await using var provider = DeviceProviderFactory.Create(
            request.Transport,
            request.VendorId,
            request.ProductId,
            loggerFactory);
        var devices = await provider.EnumerateAsync(cancellationToken)
            .ConfigureAwait(false);
        return devices
            .Where(device =>
                device.Mode != RockchipUsbMode.None &&
                (!request.VendorId.HasValue ||
                    device.VendorId == request.VendorId) &&
                (!request.ProductId.HasValue ||
                    device.ProductId == request.ProductId))
            .ToArray();
    }

    internal static async Task<DeviceSession> OpenAsync(
        ToolkitRequest request,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken cancellationToken) {
        var provider = DeviceProviderFactory.Create(
            request.Transport,
            request.VendorId,
            request.ProductId,
            loggerFactory);
        try {
            var devices = await provider.EnumerateAsync(cancellationToken)
                .ConfigureAwait(false);
            logger.LogDebug(
                "Enumerated {DeviceCount} RockUSB device(s)",
                devices.Count);
            var selected = SelectDevice(
                devices,
                request.VendorId,
                request.ProductId);
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
            return new DeviceSession(
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

    internal static async Task<RockchipUsbMode?> GetModeAsync(
        ToolkitRequest request,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        return await GetModeAsync(
                request.Transport,
                request.VendorId,
                request.ProductId,
                loggerFactory,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<RockchipUsbMode?> GetModeAsync(
        ToolkitTransport transport,
        ushort? vendorId,
        ushort? productId,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        await using var provider = DeviceProviderFactory.Create(
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
        ushort? productId) {
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
