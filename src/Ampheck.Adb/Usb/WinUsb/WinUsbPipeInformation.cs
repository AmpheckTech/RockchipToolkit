using System.Runtime.InteropServices;

namespace Ampheck.Adb.Usb.WinUsb;

[StructLayout(LayoutKind.Sequential)]
internal struct WinUsbPipeInformation {
    public WinUsbPipeType PipeType;
    public byte PipeId;
    public ushort MaximumPacketSize;
    public byte Interval;
}
