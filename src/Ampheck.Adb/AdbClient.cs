using Ampheck.Adb.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.Adb;

public sealed partial class AdbClient {
    private const UnixFileMode DefaultPushMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.GroupRead |
        UnixFileMode.OtherRead;
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbClient> _logger;

    public AdbClient(
        AdbClientOptions? options = null,
        ILoggerFactory? loggerFactory = null) {
        _options = options ?? new AdbClientOptions();
        _logger = (loggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<AdbClient>();
    }

    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(
        CancellationToken cancellationToken = default) {
        var serverAvailable =
            await AdbServerClient.IsAvailableAsync(
                    cancellationToken,
                    _options.Server)
                .ConfigureAwait(false);
        if (!serverAvailable) {
            throw new InvalidOperationException(
                "No official ADB server is listening at " +
                $"{_options.Server.Host}:{_options.Server.Port}. " +
                "Start it with 'adb start-server' or run the toolkit server.");
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

    public async Task<AdbDevice> GetDeviceAsync(
        string? serialNumber = null,
        CancellationToken cancellationToken = default) {
        var devices = await GetDevicesAsync(cancellationToken)
            .ConfigureAwait(false);
        return SelectSingle(devices, serialNumber, mustBeOnline: false);
    }

    public async Task<AdbDevice> GetOnlineDeviceAsync(
        string? serialNumber = null,
        CancellationToken cancellationToken = default) {
        var devices = await GetDevicesAsync(cancellationToken)
            .ConfigureAwait(false);
        return SelectSingle(devices, serialNumber, mustBeOnline: true);
    }

    public async Task<AdbDevice> WaitForDeviceAsync(
        string? serialNumber = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) {
        var deadline = timeout is { } duration
            ? DateTimeOffset.UtcNow + duration
            : DateTimeOffset.MaxValue;
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = await GetDevicesAsync(cancellationToken)
                .ConfigureAwait(false);
            var matching = FilterBySerial(devices, serialNumber);
            if (matching.Count > 1) {
                throw new InvalidOperationException(
                    "Multiple ADB devices were found. Specify a serial number.");
            }

            var online = matching.FirstOrDefault(device => device.IsOnline);
            if (online is not null) {
                return online;
            }

            if (DateTimeOffset.UtcNow >= deadline) {
                throw new TimeoutException(
                    serialNumber is null
                        ? "No online ADB device became available."
                        : $"ADB device '{serialNumber}' did not become online.");
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
