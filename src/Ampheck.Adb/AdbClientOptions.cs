using Ampheck.Adb.Server;

namespace Ampheck.Adb;

public sealed record AdbClientOptions {
    public AdbServerOptions Server { get; init; } = new();
}
