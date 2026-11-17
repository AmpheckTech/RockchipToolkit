using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Storage;

public sealed partial class StorageAccess {
    public async ValueTask SelectAsync(
        DeviceTarget target,
        string? loaderPath,
        StorageType storage,
        CancellationToken cancellationToken
    ) {
        await using var lease = await _access.OpenAsync(
                target,
                loaderPath,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        await lease.Device.ChangeStorageAsync(
                storage,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
