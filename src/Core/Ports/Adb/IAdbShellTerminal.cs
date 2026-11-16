namespace Ampheck.RockchipToolkit.Core.Ports.Adb;

public interface IAdbShellTerminal : IAsyncDisposable {
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default);
}
