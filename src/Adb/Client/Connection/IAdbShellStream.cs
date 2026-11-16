namespace Ampheck.RockchipToolkit.Adb.Client.Connection;

public interface IAdbShellStream : IAsyncDisposable {
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default);
}
