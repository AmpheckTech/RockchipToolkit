using Microsoft.Win32.SafeHandles;

namespace Ampheck.Adb.Usb.WinUsb;

internal sealed class SafeWinUsbHandle : SafeHandleZeroOrMinusOneIsInvalid {
    public SafeWinUsbHandle(IntPtr handle)
        : base(ownsHandle: true) {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() {
        return WinUsbNative.WinUsb_Free(handle);
    }
}
