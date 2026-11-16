using Ampheck.RockchipToolkit.Adb.Server;

namespace Ampheck.RockchipToolkit.Adb.Client;

public sealed record AdbClientOptions {
    public AdbServerOptions Server { get; init; } = new();
}
