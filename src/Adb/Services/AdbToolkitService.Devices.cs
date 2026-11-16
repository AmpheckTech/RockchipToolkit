using Ampheck.RockchipToolkit.Adb.Client;
using Ampheck.RockchipToolkit.Adb.Model;

namespace Ampheck.RockchipToolkit.Adb.Services;

internal sealed partial class AdbToolkitService {
    public async ValueTask<AdbDevicesResult> DevicesAsync(
        string? serialNumber,
        CancellationToken cancellationToken
    ) {
        await _server.EnsureRunningAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var devices = await _client.GetDevicesAsync(
                cancellationToken)
            .ConfigureAwait(false);
        return new AdbDevicesResult {
            Devices = devices
                .Where(device =>
                    serialNumber is null ||
                    device.SerialNumber.Equals(
                        serialNumber,
                        StringComparison.OrdinalIgnoreCase))
                .Select(ToDescriptor)
                .ToArray()
        };
    }

    private static AdbDeviceDescriptor ToDescriptor(
        AdbDevice device
    ) => new() {
        SerialNumber = device.SerialNumber,
        State = FormatState(device.State),
        Product = device.Product,
        Model = device.Model,
        DeviceName = device.DeviceName
    };

    private static string FormatState(AdbDeviceState state) =>
        state switch {
            AdbDeviceState.Device => "device",
            AdbDeviceState.Offline => "offline",
            AdbDeviceState.Unauthorized => "unauthorized",
            AdbDeviceState.Recovery => "recovery",
            AdbDeviceState.Sideload => "sideload",
            AdbDeviceState.Bootloader => "bootloader",
            _ => "unknown"
        };
}
