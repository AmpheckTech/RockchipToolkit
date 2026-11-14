using Ampheck.Adb.Server;

namespace Ampheck.Adb;

public sealed partial class AdbClient {
    public Task<string> ConnectNetworkAsync(
        string host,
        int port = 5555,
        CancellationToken cancellationToken = default) {
        return AdbServerClient.ExecuteHostAsync(
            $"host:connect:{host}:{port}",
            cancellationToken,
            _options.Server);
    }

    public Task<string> DisconnectNetworkAsync(
        string host,
        int port = 5555,
        CancellationToken cancellationToken = default) {
        return AdbServerClient.ExecuteHostAsync(
            $"host:disconnect:{host}:{port}",
            cancellationToken,
            _options.Server);
    }

    public Task<string> DisconnectAllNetworkAsync(
        CancellationToken cancellationToken = default) {
        return AdbServerClient.ExecuteHostAsync(
            "host:disconnect:",
            cancellationToken,
            _options.Server);
    }

    public async Task<int> GetServerVersionAsync(
        CancellationToken cancellationToken = default) {
        var value = await AdbServerClient.ExecuteHostAsync(
                "host:version",
                cancellationToken,
                _options.Server)
            .ConfigureAwait(false);
        return int.TryParse(
                value,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out var version)
            ? version
            : throw new InvalidDataException(
                "The ADB server returned an invalid version.");
    }

    public Task KillServerAsync(
        CancellationToken cancellationToken = default) {
        return AdbServerClient.KillAsync(
            cancellationToken,
            _options.Server);
    }

    public Task<string> CreateTcpForwardAsync(
        AdbDevice device,
        int localPort,
        int remotePort,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        return AdbServerClient.ExecuteForwardAsync(
            $"host-serial:{device.SerialNumber}:forward:" +
            $"tcp:{localPort};tcp:{remotePort}",
            cancellationToken,
            _options.Server);
    }

    public Task<string> ListForwardsAsync(
        CancellationToken cancellationToken = default) {
        return AdbServerClient.ExecuteHostAsync(
            "host:list-forward",
            cancellationToken,
            _options.Server);
    }

    public Task<string> RemoveTcpForwardAsync(
        AdbDevice device,
        int localPort,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        return AdbServerClient.ExecuteForwardAsync(
            $"host-serial:{device.SerialNumber}:" +
            $"killforward:tcp:{localPort}",
            cancellationToken,
            _options.Server);
    }

    public Task<string> RemoveAllForwardsAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        return AdbServerClient.ExecuteForwardAsync(
            $"host-serial:{device.SerialNumber}:killforward-all",
            cancellationToken,
            _options.Server);
    }
}
