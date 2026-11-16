using Ampheck.RockchipToolkit.Adb.Model;
using Ampheck.RockchipToolkit.Adb.Server;
using Ampheck.RockchipToolkit.Adb.Sync;

namespace Ampheck.RockchipToolkit.Adb.Client;

public sealed partial class AdbClient {
    internal Task<string> ExecuteShellAsync(
        AdbDevice device,
        string command,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ExecuteShellCommandAsync(
            device,
            command,
            cancellationToken);
    }

    internal Task<AdbShell> OpenShellAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(device);
        return OpenServerShellAsync(device, cancellationToken);
    }

    internal async Task RebootAsync(
        AdbDevice device,
        AdbRebootMode mode = AdbRebootMode.System,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(device);
        await AdbServerClient.RebootAsync(
                device.SerialNumber,
                GetServerRebootMode(mode),
                cancellationToken,
                _options.Server)
            .ConfigureAwait(false);
    }

    internal async Task PushAsync(
        AdbDevice device,
        string localPath,
        string remotePath,
        UnixFileMode mode,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(device);
        await using var transfer = await OpenSyncAsync(
                device,
                cancellationToken)
            .ConfigureAwait(false);
        await AdbSyncClient.PushAsync(
                transfer.Stream,
                localPath,
                remotePath,
                mode,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task PullAsync(
        AdbDevice device,
        string remotePath,
        string localPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(device);
        await using var transfer = await OpenSyncAsync(
                device,
                cancellationToken)
            .ConfigureAwait(false);
        await AdbSyncClient.PullAsync(
                transfer.Stream,
                remotePath,
                localPath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

}
