using Ampheck.Adb.Device;
using Ampheck.Adb.Server;
using Ampheck.Adb.Sync;

namespace Ampheck.Adb;

public sealed class AdbDevice {
    private readonly AdbClient _client;

    internal AdbDevice(
        AdbClient client,
        AdbServerDevice serverDevice) {
        _client = client;
        SerialNumber = serverDevice.SerialNumber;
        State = ParseState(serverDevice.State);
        Product = serverDevice.Product;
        Model = serverDevice.Model;
        DeviceName = serverDevice.Device;
        Backend = AdbDeviceBackend.AdbServer;
    }

    internal AdbDevice(
        AdbClient client,
        AdbDeviceInfo device,
        AdbDeviceState state) {
        _client = client;
        SerialNumber = device.SerialNumber ?? "unknown";
        State = state;
        VendorId = device.VendorId;
        ProductId = device.ProductId;
        Backend = AdbDeviceBackend.Usb;
        DirectInfo = device;
    }

    public string SerialNumber { get; }

    public AdbDeviceState State { get; }

    public string? Product { get; }

    public string? Model { get; }

    public string? DeviceName { get; }

    public ushort? VendorId { get; }

    public ushort? ProductId { get; }

    public AdbDeviceBackend Backend { get; }

    public bool IsOnline => State == AdbDeviceState.Device;

    internal AdbDeviceInfo? DirectInfo { get; }

    public Task<string> ExecuteShellAsync(
        string command,
        CancellationToken cancellationToken = default) {
        EnsureOnline();
        return _client.ExecuteShellAsync(
            this,
            command,
            cancellationToken);
    }

    public Task<AdbShell> OpenShellAsync(
        CancellationToken cancellationToken = default) {
        EnsureOnline();
        return _client.OpenShellAsync(
            this,
            cancellationToken);
    }

    public Task RebootAsync(
        AdbRebootMode mode = AdbRebootMode.System,
        CancellationToken cancellationToken = default) {
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
        CancellationToken cancellationToken = default) {
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
        CancellationToken cancellationToken = default) {
        EnsureOnline();
        return _client.PullAsync(
            this,
            remotePath,
            localPath,
            progress,
            cancellationToken);
    }

    public Task<IReadOnlyList<AdbFileInfo>> ListFilesAsync(
        string remoteDirectory,
        CancellationToken cancellationToken = default) {
        EnsureOnline();
        return _client.ListFilesAsync(
            this,
            remoteDirectory,
            cancellationToken);
    }

    public Task<AdbFileInfo> StatFileAsync(
        string remotePath,
        CancellationToken cancellationToken = default) {
        EnsureOnline();
        return _client.StatFileAsync(
            this,
            remotePath,
            cancellationToken);
    }

    public Task InstallAsync(
        string apkPath,
        AdbInstallOptions? options = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        EnsureOnline();
        return _client.InstallAsync(
            this,
            apkPath,
            options,
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
