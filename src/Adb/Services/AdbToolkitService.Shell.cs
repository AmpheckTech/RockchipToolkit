using Ampheck.RockchipToolkit.Adb.Client;
using Ampheck.RockchipToolkit.Adb.Model;

namespace Ampheck.RockchipToolkit.Adb.Services;

internal sealed partial class AdbToolkitService {
    public async ValueTask<string> ShellAsync(
        AdbShellParams parameters,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(parameters.Command)) {
            throw new InvalidDataException(
                "The ADB shell command is empty.");
        }

        await _server.EnsureRunningAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        return await device.ExecuteShellAsync(
                parameters.Command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<Core.Ports.Adb.IAdbShellTerminal> OpenShellAsync(
        string? serialNumber,
        CancellationToken cancellationToken
    ) {
        await _server.EnsureRunningAsync(cancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                serialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        return await device.OpenShellAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
