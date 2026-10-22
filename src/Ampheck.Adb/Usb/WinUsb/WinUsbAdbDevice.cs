using Microsoft.Win32.SafeHandles;

namespace Ampheck.Adb.Usb.WinUsb;

internal sealed class WinUsbAdbDevice : IDisposable {
    private readonly SafeFileHandle _deviceHandle;
    private readonly SafeWinUsbHandle _primaryInterfaceHandle;
    private readonly SafeWinUsbHandle? _associatedInterfaceHandle;
    private bool _disposed;

    public WinUsbAdbDevice(
        SafeFileHandle deviceHandle,
        SafeWinUsbHandle primaryInterfaceHandle,
        SafeWinUsbHandle? associatedInterfaceHandle,
        byte bulkInPipe,
        byte bulkOutPipe,
        ushort vendorId,
        ushort productId,
        string? serialNumber) {
        _deviceHandle = deviceHandle;
        _primaryInterfaceHandle = primaryInterfaceHandle;
        _associatedInterfaceHandle = associatedInterfaceHandle;
        BulkInPipe = bulkInPipe;
        BulkOutPipe = bulkOutPipe;
        VendorId = vendorId;
        ProductId = productId;
        SerialNumber = serialNumber;
    }

    public SafeWinUsbHandle InterfaceHandle =>
        _associatedInterfaceHandle ?? _primaryInterfaceHandle;

    public byte BulkInPipe { get; }

    public byte BulkOutPipe { get; }

    public ushort VendorId { get; }

    public ushort ProductId { get; }

    public string? SerialNumber { get; }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _associatedInterfaceHandle?.Dispose();
        _primaryInterfaceHandle.Dispose();
        _deviceHandle.Dispose();
    }
}
