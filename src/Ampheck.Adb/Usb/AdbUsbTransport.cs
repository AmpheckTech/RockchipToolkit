using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace Ampheck.Adb.Usb;

internal sealed class AdbUsbTransport : IAdbTransport {
    private readonly IUsbDevice _device;
    private readonly AdbInterfaceSelection _selection;
    private readonly AdbUsbOptions _options;
    private readonly UsbEndpointWriter _writer;
    private readonly UsbEndpointReader _reader;
    private bool _disposed;

    public AdbUsbTransport(
        IUsbDevice device,
        AdbInterfaceSelection selection,
        AdbUsbOptions options) {
        _device = device;
        _selection = selection;
        _options = options;
        if (!_device.IsOpen) {
            _device.Open();
        }

        if (!_device.IsOpen) {
            throw new IOException(
                "LibUSB could not open the ADB device.");
        }

        _device.ClaimInterface(selection.InterfaceNumber);
        _writer = _device.OpenEndpointWriter(
            (WriteEndpointID)selection.BulkOutEndpoint,
            EndpointType.Bulk);
        _reader = _device.OpenEndpointReader(
            (ReadEndpointID)selection.BulkInEndpoint,
            16 * 1024,
            EndpointType.Bulk);
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var error = _writer.Write(
            data.ToArray(),
            _options.TimeoutMilliseconds,
            out var transferred);
        if (error != Error.Success ||
            transferred != data.Length) {
            throw new IOException(
                $"ADB bulk write failed: {error}, " +
                $"transferred {transferred}/{data.Length}.");
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        // LibUSB reads are synchronous. Keep the caller free to
        // pump shell input while it waits for device output.
        return await Task.Run(
                () => Read(buffer, timeout))
            .ConfigureAwait(false);
    }

    private int Read(
        Memory<byte> buffer,
        TimeSpan? timeout) {
        var data = new byte[buffer.Length];
        var error = _reader.Read(
            data,
            ResolveTimeout(timeout),
            out var transferred);
        if (error is not (Error.Success or Error.Io)) {
            throw new IOException(
                $"ADB bulk read failed: {error}.");
        }

        data.AsSpan(0, transferred).CopyTo(buffer.Span);
        return transferred;
    }

    private int ResolveTimeout(TimeSpan? timeout) {
        if (timeout == Timeout.InfiniteTimeSpan) {
            return int.MaxValue;
        }

        if (timeout is { } duration &&
            duration > TimeSpan.Zero) {
            return duration.TotalMilliseconds >= int.MaxValue
                ? int.MaxValue
                : Math.Max(1, (int)duration.TotalMilliseconds);
        }

        return _options.TimeoutMilliseconds;
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _device.ReleaseInterface(_selection.InterfaceNumber);
        _device.Close();
        _disposed = true;
        return ValueTask.CompletedTask;
    }

}
