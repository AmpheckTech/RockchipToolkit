namespace Ampheck.Adb.Usb;

public interface IAdbTransport : IAsyncDisposable {
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
