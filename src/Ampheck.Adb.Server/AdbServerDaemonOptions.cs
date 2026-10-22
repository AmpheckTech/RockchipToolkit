using Ampheck.Adb.Usb;

namespace Ampheck.Adb.Server;

public sealed record AdbServerDaemonOptions {
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5037;
    public TimeSpan TrackDevicesInterval { get; init; } =
        TimeSpan.FromSeconds(1);
    public AdbUsbOptions Usb { get; init; } = new();
}
