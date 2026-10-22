using System.ComponentModel;
using System.Runtime.InteropServices;
using Ampheck.RockchipToolkit.RockUsb.Configuration;
using Ampheck.RockchipToolkit.RockUsb.Interop;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Win32.SafeHandles;

namespace Ampheck.RockchipToolkit.RockUsb.Transport;

internal sealed class RockUsbTransport :
    IUsbTransport,
    IMaskRomBootTransport,
    IUsbBulkSequenceTransport {
    private const uint WritePipeAccess = 0x40000000;
    private const uint WritePipeShare = 2;
    private const uint ReadPipeAccess = 0x80000000;
    private const uint ReadPipeShare = 1;
    private const int DefaultTimeoutMilliseconds = 5000;
    private readonly string _path;
    private readonly RockUsbOptions _options;
    private bool _disposed;

    private RockUsbTransport(
        string path,
        UsbDeviceInfo device,
        RockUsbOptions options) {
        _path = path;
        Device = device;
        _options = options;
    }

    public UsbDeviceInfo Device { get; }

    internal static RockUsbTransport Open(
        UsbDeviceInfo device,
        RockUsbOptions options) {
        if (options.ResetPipeOnOpen) {
            RockUsbPipeIo.Reset(
                device.PlatformId!,
                options,
                options.ResetPipeValue);
        }

        return new RockUsbTransport(
            device.PlatformId!,
            device,
            options);
    }

    public ValueTask WriteBulkAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();

        using var handle = OpenWritePipe();
        WriteToHandle(handle, data);
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteBulkSequenceAsync(
        ReadOnlyMemory<byte> first,
        ReadOnlyMemory<byte> second,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();

        using var handle = OpenWritePipe();
        WriteToHandle(handle, first);
        if (!second.IsEmpty) {
            WriteToHandle(handle, second);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadBulkAsync(
        Memory<byte> buffer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();

        var task = Task.Run(
            () => ReadPipe(buffer.Length),
            CancellationToken.None);
        var data = await task.WaitAsync(
                ResolveTimeout(timeout),
                cancellationToken)
            .ConfigureAwait(false);
        data.CopyTo(buffer.Span);
        return data.Length;
    }

    public ValueTask<int> ControlTransferAsync(
        UsbControlRequest request,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        throw new NotSupportedException(
            "rockusb.sys exposes vendor requests through private IOCTLs.");
    }

    public ValueTask ResetAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        RockUsbPipeIo.Reset(
            Device.PlatformId!,
            _options,
            _options.ResetPipeValue);
        return ValueTask.CompletedTask;
    }

    public ValueTask DownloadBootEntryAsync(
        ushort requestCode,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen();
        RockUsbBootIo.DownloadEntry(
            _path,
            _options,
            requestCode,
            data.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() {
        if (_disposed) {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private byte[] ReadPipe(int length) {
        using var handle = OpenReadPipe();
        var bytes = new byte[length];
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try {
            if (!RockUsbNative.ReadFile(
                    handle,
                    pin.AddrOfPinnedObject(),
                    checked((uint)bytes.Length),
                    out var read,
                    IntPtr.Zero)) {
                throw NativeError("ReadFile");
            }

            return bytes.AsSpan(0, checked((int)read)).ToArray();
        } finally {
            pin.Free();
        }
    }

    private static void WriteToHandle(
        SafeFileHandle handle,
        ReadOnlyMemory<byte> data) {
        var bytes = data.ToArray();
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            if (!RockUsbNative.WriteFile(
                    handle,
                    pointer,
                    checked((uint)bytes.Length),
                    out var written,
                    IntPtr.Zero)) {
                throw NativeError(
                    $"WriteFile (requested {bytes.Length}, written {written})");
            }

            if (written != bytes.Length) {
                throw new IOException(
                    $"WriteFile returned a short write " +
                    $"(requested {bytes.Length}, written {written}).");
            }

        } finally {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private SafeFileHandle OpenWritePipe() {
        var handle = RockUsbPipeIo.OpenPipe(
            _path,
            "\\PIPE01",
            WritePipeAccess,
            WritePipeShare);
        if (handle.IsInvalid) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to open the rockusb.sys write pipe.");
        }

        return handle;
    }

    private SafeFileHandle OpenReadPipe() {
        var handle = RockUsbPipeIo.OpenPipe(
            _path,
            "\\PIPE00",
            ReadPipeAccess,
            ReadPipeShare);
        if (handle.IsInvalid) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to open the rockusb.sys read pipe.");
        }

        return handle;
    }

    private TimeSpan ResolveTimeout(TimeSpan timeout) {
        return timeout > TimeSpan.Zero
            ? timeout
            : TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds);
    }

    private static Win32Exception NativeError(string operation) {
        var error = Marshal.GetLastWin32Error();
        return new Win32Exception(
            error,
            $"{operation} failed (Win32 {error}).");
    }

    private void EnsureOpen() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
