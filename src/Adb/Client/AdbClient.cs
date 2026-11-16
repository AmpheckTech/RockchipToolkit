using Ampheck.RockchipToolkit.Adb.Server;
using Ampheck.RockchipToolkit.Adb.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.Adb.Client;

public sealed partial class AdbClient {
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbClient> _logger;

    public AdbClient(
        AdbClientOptions? options = null,
        ILoggerFactory? loggerFactory = null
    ) {
        _options = options ?? new AdbClientOptions();
        _logger = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<AdbClient>();
    }

    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(
        CancellationToken cancellationToken = default
    ) {
        var serverAvailable =
            await AdbServerClient.IsAvailableAsync(
                    cancellationToken,
                    _options.Server)
                .ConfigureAwait(false);
        if (!serverAvailable) {
            throw new InvalidOperationException(
                "No official ADB server is listening at " +
                $"{_options.Server.Host}:{_options.Server.Port}. " +
                "Start it with 'adb start-server'.");
        }

        _logger.LogDebug(
            "Enumerating ADB devices through {Host}:{Port}",
            _options.Server.Host,
            _options.Server.Port);
        var devices = await AdbServerClient.ListDevicesAsync(
                cancellationToken,
                _options.Server)
            .ConfigureAwait(false);
        return devices
            .Select(device => new AdbDevice(this, device))
            .ToArray();
    }

    public async Task<AdbDevice> GetOnlineDeviceAsync(
        string? serialNumber = null,
        CancellationToken cancellationToken = default
    ) {
        var devices = await GetDevicesAsync(cancellationToken)
            .ConfigureAwait(false);
        return SelectSingle(devices, serialNumber, mustBeOnline: true);
    }
}
