namespace Ampheck.Adb.Server;

public sealed record AdbServerOptions {
    public string Host { get; init; } = AdbDefaults.ServerHost;
    public int Port { get; init; } = AdbDefaults.ServerPort;
    public TimeSpan ConnectTimeout { get; init; } =
        TimeSpan.FromMilliseconds(500);
}
