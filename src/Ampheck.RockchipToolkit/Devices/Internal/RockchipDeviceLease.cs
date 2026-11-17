using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Devices.Internal;

internal sealed class RockchipDeviceLease(
    RockchipDevice device,
    IUsbDeviceProvider provider
) {

    public RockchipDevice Device { get; } = device;

    public async ValueTask DisposeAsync() {
        await Device.DisposeAsync().ConfigureAwait(false);
        await provider.DisposeAsync().ConfigureAwait(false);
    }
}
