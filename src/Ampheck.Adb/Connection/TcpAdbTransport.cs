using System.Net.Sockets;
using Ampheck.Adb.Usb;

namespace Ampheck.Adb.Connection;

public sealed class TcpAdbTransport : IAdbTransport {
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private bool _disposed;

    private TcpAdbTransport(
        TcpClient client,
        NetworkStream stream) {
        _client = client;
        _stream = stream;
    }

    public static async ValueTask<TcpAdbTransport> ConnectAsync(
        string host,
        int port,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var client = new TcpClient();
        using var connectTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        connectTimeout.CancelAfter(
            timeout ?? TimeSpan.FromSeconds(10));
        try {
            await client.ConnectAsync(
                    host,
                    port,
                    connectTimeout.Token)
                .ConfigureAwait(false);
            client.NoDelay = true;
            return new TcpAdbTransport(
                client,
                client.GetStream());
        } catch {
            client.Dispose();
            throw;
        }
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _stream.WriteAsync(data, cancellationToken);
    }

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (timeout is null ||
            timeout == Timeout.InfiniteTimeSpan) {
            return await _stream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
        }

        using var readTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        readTimeout.CancelAfter(timeout.Value);
        return await _stream.ReadAsync(buffer, readTimeout.Token)
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _stream.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
