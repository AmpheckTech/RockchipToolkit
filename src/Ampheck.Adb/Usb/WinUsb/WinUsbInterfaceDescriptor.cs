using System.Runtime.InteropServices;

namespace Ampheck.Adb.Usb.WinUsb;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WinUsbInterfaceDescriptor {
    public byte Length;
    public byte DescriptorType;
    public byte InterfaceNumber;
    public byte AlternateSetting;
    public byte EndpointCount;
    public byte InterfaceClass;
    public byte InterfaceSubClass;
    public byte InterfaceProtocol;
    public byte InterfaceStringIndex;
}
