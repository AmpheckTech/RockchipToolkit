using Ampheck.Adb.Server;
using Ampheck.Adb.Usb;

namespace Ampheck.Adb;

public sealed record AdbClientOptions {
    public AdbBackendPreference Backend { get; init; } =
        AdbBackendPreference.Auto;
    public AdbServerOptions Server { get; init; } = new();
    public AdbUsbOptions Usb { get; init; } = new();
}
