using Ampheck.RockchipToolkit.Adb.Model;

namespace Ampheck.RockchipToolkit.Adb.Services;

internal sealed partial class AdbToolkitService {
    public async ValueTask EnsureServerAsync(
        CancellationToken cancellationToken
    ) {
        await _server.EnsureRunningAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask RebootAsync(
        AdbRebootParams parameters,
        CancellationToken cancellationToken
    ) {
        await _server.EnsureRunningAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        await device.RebootAsync(
                parameters.Mode,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
