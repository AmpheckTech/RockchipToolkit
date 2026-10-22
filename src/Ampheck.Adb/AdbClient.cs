using System.Text;
using Ampheck.Adb.Connection;
using Ampheck.Adb.Device;
using Ampheck.Adb.Server;
using Ampheck.Adb.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.Adb;

public sealed class AdbClient {
    private const UnixFileMode DefaultPushMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.GroupRead |
        UnixFileMode.OtherRead;
    private readonly AdbClientOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AdbClient> _logger;

    public AdbClient(
        AdbClientOptions? options = null,
        ILoggerFactory? loggerFactory = null) {
        _options = options ?? new AdbClientOptions();
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<AdbClient>();
    }

    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(
        CancellationToken cancellationToken = default) {
        if (_options.Backend != AdbBackendPreference.Usb) {
            var serverAvailable =
                await AdbServerClient.IsAvailableAsync(
                        cancellationToken,
                        _options.Server)
                    .ConfigureAwait(false);
            if (!serverAvailable &&
                _options.Backend ==
                    AdbBackendPreference.AdbServer) {
                throw new InvalidOperationException(
                    "The ADB backend was set to AdbServer, " +
                    "but no server is listening at " +
                    $"{_options.Server.Host}:{_options.Server.Port}.");
            }

            if (serverAvailable) {
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
        }

        return await EnumerateUsbDevicesAsync(cancellationToken)
            .ConfigureAwait(false);
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

    internal Task<string> ExecuteShellAsync(
        AdbDevice device,
        string command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ExecuteShellCommandAsync(
            device,
            command,
            cancellationToken);
    }

    internal Task<AdbShell> OpenShellAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        return device.Backend == AdbDeviceBackend.AdbServer
            ? OpenServerShellAsync(device, cancellationToken)
            : OpenUsbShellAsync(device, cancellationToken);
    }

    internal async Task RebootAsync(
        AdbDevice device,
        AdbRebootMode mode = AdbRebootMode.System,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        if (device.Backend == AdbDeviceBackend.AdbServer) {
            await AdbServerClient.RebootAsync(
                    device.SerialNumber,
                    GetServerRebootMode(mode),
                    cancellationToken,
                    _options.Server)
                .ConfigureAwait(false);
            return;
        }

        var command = mode == AdbRebootMode.System
            ? "reboot"
            : $"reboot {GetServerRebootMode(mode)}";
        await RebootUsbAsync(
                device,
                command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task PushAsync(
        AdbDevice device,
        string localPath,
        string remotePath,
        UnixFileMode mode,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
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
        CancellationToken cancellationToken = default) {
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

    internal async Task<IReadOnlyList<AdbFileInfo>> ListFilesAsync(
        AdbDevice device,
        string remoteDirectory,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        await using var transfer = await OpenSyncAsync(
                device,
                cancellationToken)
            .ConfigureAwait(false);
        return await AdbSyncClient.ListDirectoryAsync(
                transfer.Stream,
                remoteDirectory,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<AdbFileInfo> StatFileAsync(
        AdbDevice device,
        string remotePath,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        await using var transfer = await OpenSyncAsync(
                device,
                cancellationToken)
            .ConfigureAwait(false);
        return await AdbSyncClient.StatFileAsync(
                transfer.Stream,
                remotePath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task InstallAsync(
        AdbDevice device,
        string apkPath,
        AdbInstallOptions? installOptions,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        installOptions ??= new AdbInstallOptions();
        if (installOptions.UserId is < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(installOptions),
                "The Android user ID cannot be negative.");
        }

        var remotePath =
            $"/data/local/tmp/ampheck-{Guid.NewGuid():N}.apk";
        try {
            await PushAsync(
                    device,
                    apkPath,
                    remotePath,
                    DefaultPushMode,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            var arguments = new List<string> {
                "pm",
                "install"
            };
            if (installOptions.ReplaceExisting) {
                arguments.Add("-r");
            }

            if (installOptions.AllowDowngrade) {
                arguments.Add("-d");
            }

            if (installOptions.GrantPermissions) {
                arguments.Add("-g");
            }

            if (installOptions.UserId is { } userId) {
                arguments.Add("--user");
                arguments.Add(userId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            arguments.Add($"\"{remotePath}\"");
            var output = await ExecuteShellCommandAsync(
                    device,
                    string.Join(" ", arguments),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!output.Contains(
                    "Success",
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(output)
                        ? "APK installation failed."
                        : output.Trim());
            }
        } finally {
            try {
                await ExecuteShellCommandAsync(
                        device,
                        $"rm -f \"{remotePath}\"",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            } catch (Exception error) when (
                error is IOException or InvalidOperationException) {
                _logger.LogWarning(
                    error,
                    "Unable to remove temporary APK {RemotePath}",
                    remotePath);
            }
        }
    }

    private async Task<IReadOnlyList<AdbDevice>> EnumerateUsbDevicesAsync(
        CancellationToken cancellationToken) {
        _logger.LogDebug(
            "Enumerating ADB devices through the local USB backend");
        await using var provider = AdbDeviceProviderFactory.Create(
            _loggerFactory,
            _options.Usb);
        var directDevices = await provider.EnumerateAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var result = new List<AdbDevice>(directDevices.Count);
        foreach (var info in directDevices) {
            cancellationToken.ThrowIfCancellationRequested();
            var state = AdbDeviceState.Offline;
            AdbConnection? connection = null;
            try {
                connection = await provider.OpenAsync(
                        info,
                        cancellationToken)
                    .ConfigureAwait(false);
                var connectionState = await connection.ConnectAsync(
                        waitForAuthorization: false,
                        status: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                state = connectionState == AdbConnectionState.Online
                    ? AdbDeviceState.Device
                    : AdbDeviceState.Unauthorized;
            } catch (Exception error) when (
                error is IOException or
                InvalidOperationException or
                UnauthorizedAccessException or
                System.ComponentModel.Win32Exception) {
                _logger.LogDebug(
                    error,
                    "Unable to probe ADB state for {SerialNumber}",
                    info.SerialNumber ?? "unknown");
            } finally {
                if (connection is not null) {
                    await connection.DisposeAsync()
                        .ConfigureAwait(false);
                }
            }

            result.Add(new AdbDevice(this, info, state));
        }

        return result;
    }

    private async Task<AdbShell> OpenServerShellAsync(
        AdbDevice device,
        CancellationToken cancellationToken) {
        var shell = await AdbServerClient.OpenShellAsync(
                device.SerialNumber,
                command: null,
                cancellationToken,
                _options.Server)
            .ConfigureAwait(false);
        return new AdbShell(shell);
    }

    private async Task<AdbShell> OpenUsbShellAsync(
        AdbDevice device,
        CancellationToken cancellationToken) {
        var direct = RequireDirectInfo(device);
        var provider = AdbDeviceProviderFactory.Create(
            _loggerFactory,
            _options.Usb);
        AdbConnection? connection = null;
        try {
            connection = await provider.OpenAsync(
                    direct,
                    cancellationToken)
                .ConfigureAwait(false);
            await ConnectDirectAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            var shell = await connection.OpenShellAsync(
                    command: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbShell(shell, connection, provider);
        } catch {
            if (connection is not null) {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> ExecuteShellCommandAsync(
        AdbDevice device,
        string command,
        CancellationToken cancellationToken) {
        if (device.Backend == AdbDeviceBackend.AdbServer) {
            await using var serverShell =
                await AdbServerClient.OpenShellAsync(
                        device.SerialNumber,
                        command,
                        cancellationToken,
                        _options.Server)
                    .ConfigureAwait(false);
            var output = new StringBuilder();
            await serverShell.ReadAsync(
                    text => output.Append(text),
                    cancellationToken)
                .ConfigureAwait(false);
            return output.ToString();
        }

        await using var shell = await OpenUsbShellCommandAsync(
                device,
                command,
                cancellationToken)
            .ConfigureAwait(false);
        var directOutput = new StringBuilder();
        await shell.ReadAsync(
                text => directOutput.Append(text),
                cancellationToken)
            .ConfigureAwait(false);
        return directOutput.ToString();
    }

    private async Task<AdbShell> OpenUsbShellCommandAsync(
        AdbDevice device,
        string command,
        CancellationToken cancellationToken) {
        var direct = RequireDirectInfo(device);
        var provider = AdbDeviceProviderFactory.Create(
            _loggerFactory,
            _options.Usb);
        AdbConnection? connection = null;
        try {
            connection = await provider.OpenAsync(
                    direct,
                    cancellationToken)
                .ConfigureAwait(false);
            await ConnectDirectAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            var shell = await connection.OpenShellAsync(
                    command,
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbShell(shell, connection, provider);
        } catch {
            if (connection is not null) {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task RebootUsbAsync(
        AdbDevice device,
        string command,
        CancellationToken cancellationToken) {
        await using var shell = await OpenUsbShellCommandAsync(
                device,
                command,
                cancellationToken)
            .ConfigureAwait(false);
        try {
            await shell.ReadAsync(
                    _ => {
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        } catch (IOException error) {
            _logger.LogDebug(
                error,
                "ADB disconnected after reboot request");
        }
    }

    private async Task<AdbSyncTransfer> OpenSyncAsync(
        AdbDevice device,
        CancellationToken cancellationToken) {
        if (device.Backend == AdbDeviceBackend.AdbServer) {
            var stream = await AdbServerClient.OpenSyncAsync(
                    device.SerialNumber,
                    cancellationToken,
                    _options.Server)
                .ConfigureAwait(false);
            return new AdbSyncTransfer(stream);
        }

        var direct = RequireDirectInfo(device);
        var provider = AdbDeviceProviderFactory.Create(
            _loggerFactory,
            _options.Usb);
        AdbConnection? connection = null;
        try {
            connection = await provider.OpenAsync(
                    direct,
                    cancellationToken)
                .ConfigureAwait(false);
            await ConnectDirectAsync(connection, cancellationToken)
                .ConfigureAwait(false);
            var stream = await connection.OpenServiceAsync(
                    "sync:",
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbSyncTransfer(
                stream,
                connection,
                provider);
        } catch {
            if (connection is not null) {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            await provider.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static AdbDevice SelectSingle(
        IEnumerable<AdbDevice> devices,
        string? serialNumber,
        bool mustBeOnline) {
        var matching = FilterBySerial(devices, serialNumber)
            .Where(device => !mustBeOnline || device.IsOnline)
            .ToArray();
        if (matching.Length == 0) {
            throw new InvalidOperationException(
                serialNumber is null
                    ? mustBeOnline
                        ? "No online ADB device was found."
                        : "No ADB device was found."
                    : $"ADB device '{serialNumber}' " +
                      (mustBeOnline
                          ? "is not online."
                          : "was not found."));
        }

        if (matching.Length > 1) {
            throw new InvalidOperationException(
                "Multiple ADB devices were found. Specify a serial number.");
        }

        return matching[0];
    }

    private static IReadOnlyList<AdbDevice> FilterBySerial(
        IEnumerable<AdbDevice> devices,
        string? serialNumber) {
        return serialNumber is null
            ? devices.ToArray()
            : devices
                .Where(device => device.SerialNumber.Equals(
                    serialNumber,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
    }

    private static AdbDeviceInfo RequireDirectInfo(
        AdbDevice device) {
        return device.DirectInfo ??
            throw new InvalidOperationException(
                "The USB device information is unavailable.");
    }

    private static async Task ConnectDirectAsync(
        AdbConnection connection,
        CancellationToken cancellationToken) {
        var state = await connection.ConnectAsync(
                waitForAuthorization: true,
                status: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (state != AdbConnectionState.Online) {
            throw new InvalidOperationException(
                "ADB authorization was not granted.");
        }
    }

    private static string GetServerRebootMode(AdbRebootMode mode) {
        return mode switch {
            AdbRebootMode.System => string.Empty,
            AdbRebootMode.Bootloader => "bootloader",
            AdbRebootMode.Recovery => "recovery",
            AdbRebootMode.Sideload => "sideload",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
