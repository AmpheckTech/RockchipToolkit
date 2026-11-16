using Ampheck.RockchipToolkit.Adb.Model;
using Ampheck.RockchipToolkit.Adb.Server;

namespace Ampheck.RockchipToolkit.Adb.Client;

public sealed class AdbDevice {
    private readonly AdbClient _client;

    internal AdbDevice(
        AdbClient client,
        AdbServerDevice serverDevice
    ) {
        _client = client;
        SerialNumber = serverDevice.SerialNumber;
        State = ParseState(serverDevice.State);
        Product = serverDevice.Product;
        Model = serverDevice.Model;
        DeviceName = serverDevice.Device;
    }

    public string SerialNumber { get; }

    public AdbDeviceState State { get; }

    public string? Product { get; }

    public string? Model { get; }

    public string? DeviceName { get; }

    public bool IsOnline => State == AdbDeviceState.Device;

    public Task<string> ExecuteShellAsync(
        string command,
        CancellationToken cancellationToken = default
    ) {
        EnsureOnline();
        return _client.ExecuteShellAsync(
            this,
            command,
            cancellationToken);
    }

    public Task<AdbShell> OpenShellAsync(
        CancellationToken cancellationToken = default
    ) {
        EnsureOnline();
        return _client.OpenShellAsync(
            this,
            cancellationToken);
    }

    public Task RebootAsync(
        AdbRebootMode mode = AdbRebootMode.System,
        CancellationToken cancellationToken = default
    ) {
        EnsureOnline();
        return _client.RebootAsync(this, mode, cancellationToken);
    }

    public Task PushAsync(
        string localPath,
        string remotePath,
        UnixFileMode mode =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.GroupRead |
            UnixFileMode.OtherRead,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        EnsureOnline();
        return _client.PushAsync(
            this,
            localPath,
            remotePath,
            mode,
            progress,
            cancellationToken);
    }

    public Task PullAsync(
        string remotePath,
        string localPath,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        EnsureOnline();
        return _client.PullAsync(
            this,
            remotePath,
            localPath,
            progress,
            cancellationToken);
    }

    private void EnsureOnline() {
        if (!IsOnline) {
            throw new InvalidOperationException(
                $"ADB device '{SerialNumber}' is not online.");
        }
    }

    private static AdbDeviceState ParseState(string state) {
        return state switch {
            "device" => AdbDeviceState.Device,
            "offline" => AdbDeviceState.Offline,
            "unauthorized" => AdbDeviceState.Unauthorized,
            "recovery" => AdbDeviceState.Recovery,
            "sideload" => AdbDeviceState.Sideload,
            "bootloader" => AdbDeviceState.Bootloader,
            _ => AdbDeviceState.Unknown
        };
    }
}
