namespace Ampheck.Adb.Connection;

public interface IAdbShellStream : IAsyncDisposable {
    ValueTask WriteAsync(
        string text,
        CancellationToken cancellationToken = default);

    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    ValueTask ResizeAsync(
        int columns,
        int rows,
        CancellationToken cancellationToken = default);

    Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default,
        TimeSpan? readTimeout = null);
}
