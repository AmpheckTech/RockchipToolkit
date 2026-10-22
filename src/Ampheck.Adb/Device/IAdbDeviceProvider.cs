using Ampheck.Adb.Connection;

namespace Ampheck.Adb.Device;

public interface IAdbDeviceProvider : IAsyncDisposable {
    ValueTask<IReadOnlyList<AdbDeviceInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default);

    ValueTask<AdbConnection> OpenAsync(
        AdbDeviceInfo device,
        CancellationToken cancellationToken = default);
}
