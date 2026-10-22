namespace Ampheck.Adb.Usb;

public sealed record AdbUsbOptions {
    public int TimeoutMilliseconds { get; init; } = 10_000;
    public uint MaxPayloadBytes { get; init; } = 256 * 1024;
}
