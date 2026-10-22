using Ampheck.RockchipToolkit.Transport;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace Ampheck.RockchipToolkit.LibUsb.Transport;

internal sealed class LibUsbTransport : IUsbTransport {
    private readonly IUsbDevice _device;
    private readonly LibUsbOptions _options;
    private readonly LibUsbEndpointSelection _selection;
    private bool _claimed;
    private bool _disposed;

    public LibUsbTransport(
        IUsbDevice device,
        UsbDeviceInfo info,
        LibUsbOptions options) {
        _device = device;
        _options = options;
        Device = info;

        if (!_device.IsOpen) {
            _device.Open();
        }

        if (!_device.IsOpen) {
            throw new IOException(
                "LibUSB could not open the device handle.");
        }

        _selection = LibUsbEndpointSelector.Select(
            _device,
            _options);
        if (_options.ClaimInterface) {
            _device.ClaimInterface(_selection.InterfaceNumber);
            _claimed = true;
        }
    }

    public UsbDeviceInfo Device { get; }

    public ValueTask WriteBulkAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();

        var writer = _device.OpenEndpointWriter(
            (WriteEndpointID)_selection.BulkOut,
            EndpointType.Bulk);
        var error = writer.Write(
            data.ToArray(),
            _options.TimeoutMilliseconds,
            out var transferred);
        if (error != Error.Success ||
            transferred != data.Length) {
            throw new IOException(
                $"LibUSB bulk write failed: {error}, " +
                $"transferred {transferred}/{data.Length}, " +
                $"interface {_selection.InterfaceNumber}, " +
                $"endpoint 0x{_selection.BulkOut:X2}.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<int> ReadBulkAsync(
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();

        var data = new byte[buffer.Length];
        var reader = _device.OpenEndpointReader(
            (ReadEndpointID)_selection.BulkIn,
            buffer.Length,
            EndpointType.Bulk);
        var error = reader.Read(
            data,
            ResolveTimeout(timeout),
            out var transferred);

        if (error is not (Error.Success or Error.Io)) {
            throw new IOException(
                $"LibUSB bulk read failed: {error}, " +
                $"interface {_selection.InterfaceNumber}, " +
                $"endpoint 0x{_selection.BulkIn:X2}.");
        }

        if (transferred > buffer.Length) {
            throw new IOException(
                "LibUSB returned more data than requested.");
        }

        data.AsSpan(0, transferred).CopyTo(buffer.Span);
        return ValueTask.FromResult(transferred);
    }

    public ValueTask<int> ControlTransferAsync(
        UsbControlRequest request,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        ValidateControlTransfer(request, buffer);

        var setup = new UsbSetupPacket(
            request.RequestType,
            request.Request,
            request.Value,
            request.Index,
            checked((int)request.Length));
        var data = buffer[..checked((int)request.Length)].ToArray();
        var transferred = _device.ControlTransfer(
            setup,
            data,
            0,
            data.Length);
        if (transferred < 0) {
            throw new IOException(
                $"LibUSB control transfer failed: {transferred}.");
        }

        if (transferred > buffer.Length) {
            throw new IOException(
                "LibUSB returned more control data than requested.");
        }

        data.AsSpan(0, transferred).CopyTo(buffer.Span);
        return ValueTask.FromResult(transferred);
    }

    public ValueTask ResetAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        _device.ResetDevice();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        if (_claimed) {
            _device.ReleaseInterface(_selection.InterfaceNumber);
        }

        _device.Close();
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private int ResolveTimeout(TimeSpan timeout) {
        if (timeout <= TimeSpan.Zero) {
            return _options.TimeoutMilliseconds;
        }

        return timeout.TotalMilliseconds >= int.MaxValue
            ? int.MaxValue
            : Math.Max(1, (int)timeout.TotalMilliseconds);
    }

    private static void ValidateControlTransfer(
        UsbControlRequest request,
        Memory<byte> buffer) {
        if (request.Length > buffer.Length) {
            throw new ArgumentException(
                "The control-transfer buffer is smaller than the declared transfer length.",
                nameof(buffer));
        }

        if (request.Length > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "USB control transfers cannot exceed 65535 bytes.");
        }
    }

    private void EnsureOpen() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
