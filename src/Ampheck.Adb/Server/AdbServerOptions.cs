namespace Ampheck.Adb.Server;

public sealed record AdbServerOptions {
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5037;
    public TimeSpan ConnectTimeout { get; init; } =
        TimeSpan.FromMilliseconds(500);
}
