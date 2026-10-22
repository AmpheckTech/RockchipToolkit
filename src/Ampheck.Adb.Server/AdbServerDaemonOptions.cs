using Ampheck.Adb.Usb;

namespace Ampheck.Adb.Server;

public sealed record AdbServerDaemonOptions {
    public string Host { get; init; } = AdbDefaults.ServerHost;
    public int Port { get; init; } = AdbDefaults.ServerPort;
    public TimeSpan TrackDevicesInterval { get; init; } =
        TimeSpan.FromSeconds(1);
    public AdbUsbOptions Usb { get; init; } = new();
}
