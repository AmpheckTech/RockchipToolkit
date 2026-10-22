using Ampheck.RockchipToolkit.Transport;

namespace Ampheck.RockchipToolkit.Server.Devices;

internal sealed class DeviceSession(
    IUsbTransport transport,
    IUsbDeviceProvider provider) : IAsyncDisposable {
    public IUsbTransport Transport { get; } = transport;

    public ValueTask DisposeAsync() => provider.DisposeAsync();
}
