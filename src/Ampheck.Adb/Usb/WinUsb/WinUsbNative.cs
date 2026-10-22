using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Ampheck.Adb.Usb.WinUsb;

internal static class WinUsbNative {
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint ShareRead = 1;
    internal const uint ShareWrite = 2;
    internal const uint OpenExisting = 3;
    internal const uint FileAttributeNormal = 0x80;
    internal const uint FileFlagOverlapped = 0x40000000;
    internal const uint PipeTransferTimeout = 0x03;
    internal const uint PresentDeviceInterfaces = 1;
    internal const int ErrorSemTimeout = 121;
    internal const int ErrorOperationAborted = 995;
    internal const byte DeviceDescriptorType = 1;
    internal const byte StringDescriptorType = 3;

    internal static readonly Guid UsbDeviceInterface =
        new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    internal static readonly Guid AndroidAdbInterface =
        new("F72FE0D4-CBCB-407D-8814-9ED673D0DD6B");
    internal static readonly Guid AndroidUsbInterface =
        new("DEE824EF-729B-4A0E-9C14-B7117D33A817");

    [DllImport(
        "CfgMgr32.dll",
        CharSet = CharSet.Unicode)]
    internal static extern int CM_Get_Device_Interface_List_Size(
        out uint length,
        ref Guid interfaceClassGuid,
        string? deviceId,
        uint flags);

    [DllImport(
        "CfgMgr32.dll",
        CharSet = CharSet.Unicode)]
    internal static extern int CM_Get_Device_Interface_List(
        ref Guid interfaceClassGuid,
        string? deviceId,
        StringBuilder buffer,
        uint bufferLength,
        uint flags);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string path,
        uint access,
        uint share,
        IntPtr security,
        uint creation,
        uint flags,
        IntPtr template);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_Initialize(
        SafeFileHandle deviceHandle,
        out IntPtr interfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_Free(IntPtr interfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_GetAssociatedInterface(
        IntPtr interfaceHandle,
        byte associatedInterfaceIndex,
        out IntPtr associatedInterfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_QueryInterfaceSettings(
        IntPtr interfaceHandle,
        byte alternateSetting,
        out WinUsbInterfaceDescriptor descriptor);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_QueryPipe(
        IntPtr interfaceHandle,
        byte alternateSetting,
        byte pipeIndex,
        out WinUsbPipeInformation pipeInformation);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_SetPipePolicy(
        IntPtr interfaceHandle,
        byte pipeId,
        uint policyType,
        uint valueLength,
        ref uint value);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_ReadPipe(
        IntPtr interfaceHandle,
        byte pipeId,
        byte[] buffer,
        uint bufferLength,
        out uint transferred,
        IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_WritePipe(
        IntPtr interfaceHandle,
        byte pipeId,
        byte[] buffer,
        uint bufferLength,
        out uint transferred,
        IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    internal static extern bool WinUsb_GetDescriptor(
        IntPtr interfaceHandle,
        byte descriptorType,
        byte index,
        ushort languageId,
        byte[] buffer,
        uint bufferLength,
        out uint transferred);
}
