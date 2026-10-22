using System.Runtime.InteropServices;

namespace Ampheck.Adb.Usb.WinUsb;

[StructLayout(LayoutKind.Sequential)]
internal struct SetupDeviceInfoData {
    public uint Size;
    public Guid ClassGuid;
    public uint DeviceInstance;
    public IntPtr Reserved;
}
