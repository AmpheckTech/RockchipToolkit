using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ampheck.Adb.Usb.WinUsb;

internal sealed class WinUsbAdbTransport : IAdbTransport {
    private readonly WinUsbAdbDevice _device;
    private readonly AdbUsbOptions _options;
    private bool _disposed;

    public WinUsbAdbTransport(
        WinUsbAdbDevice device,
        AdbUsbOptions options) {
        _device = device;
        _options = options;
        SetTimeout(
            device.BulkInPipe,
            options.TimeoutMilliseconds);
        SetTimeout(
            device.BulkOutPipe,
            options.TimeoutMilliseconds);
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var buffer = data.ToArray();
        if (!WinUsbNative.WinUsb_WritePipe(
                _device.InterfaceHandle.DangerousGetHandle(),
                _device.BulkOutPipe,
                buffer,
                checked((uint)buffer.Length),
                out var transferred,
                IntPtr.Zero)) {
            throw NativeError("WinUSB bulk write");
        }

        if (transferred != buffer.Length) {
            throw new IOException(
                $"WinUSB bulk write transferred " +
                $"{transferred}/{buffer.Length} bytes.");
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        // WinUSB reads are synchronous. Keep the caller free to
        // pump shell input while it waits for device output.
        return await Task.Run(
                () => Read(buffer, timeout))
            .ConfigureAwait(false);
    }

    private int Read(
        Memory<byte> buffer,
        TimeSpan? timeout) {
        if (timeout is { } customTimeout) {
            SetTimeout(
                _device.BulkInPipe,
                ResolveTimeout(customTimeout));
        }

        var data = new byte[buffer.Length];
        if (!WinUsbNative.WinUsb_ReadPipe(
                _device.InterfaceHandle.DangerousGetHandle(),
                _device.BulkInPipe,
                data,
                checked((uint)data.Length),
                out var transferred,
                IntPtr.Zero)) {
            throw NativeError("WinUSB bulk read");
        }

        if (transferred > buffer.Length) {
            throw new IOException(
                "WinUSB returned more data than requested.");
        }

        data.AsSpan(0, checked((int)transferred))
            .CopyTo(buffer.Span);
        return checked((int)transferred);
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _device.Dispose();
        return ValueTask.CompletedTask;
    }

    private void SetTimeout(byte pipe, int milliseconds) {
        var value = checked((uint)milliseconds);
        if (!WinUsbNative.WinUsb_SetPipePolicy(
                _device.InterfaceHandle.DangerousGetHandle(),
                pipe,
                WinUsbNative.PipeTransferTimeout,
                sizeof(uint),
                ref value)) {
            throw NativeError(
                $"WinUSB timeout policy for pipe 0x{pipe:X2}");
        }
    }

    private int ResolveTimeout(TimeSpan timeout) {
        if (timeout == Timeout.InfiniteTimeSpan) {
            return int.MaxValue;
        }

        if (timeout > TimeSpan.Zero) {
            return timeout.TotalMilliseconds >= int.MaxValue
                ? int.MaxValue
                : Math.Max(
                    1,
                    checked((int)timeout.TotalMilliseconds));
        }

        return _options.TimeoutMilliseconds;
    }

    private static Exception NativeError(string operation) {
        var error = Marshal.GetLastWin32Error();
        return error is
            WinUsbNative.ErrorSemTimeout or
            WinUsbNative.ErrorOperationAborted
                ? new TimeoutException(
                    $"{operation} timed out (Win32 {error}).")
                : new IOException(
                    $"{operation} failed (Win32 {error}).");
    }
}
