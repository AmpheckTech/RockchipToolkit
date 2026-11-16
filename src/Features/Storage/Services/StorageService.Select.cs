using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Storage.Model;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;

namespace Ampheck.RockchipToolkit.Features.Storage.Services;

internal sealed partial class StorageService {
    public async ValueTask SelectAsync(
        StorageSelectParams parameters,
        CancellationToken cancellationToken
    ) {
        await using var lease = await access.OpenAsync(
                parameters.Target,
                parameters.LoaderLocation,
                progress: null,
                cancellationToken)
            .ConfigureAwait(false);
        await lease.Device.ChangeStorageAsync(
                parameters.Storage,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
