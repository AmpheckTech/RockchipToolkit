using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Ampheck.RockchipToolkit.RockUsb.Interop;

internal static class RockUsbNative {
    internal const uint Present = 0;
    internal const int CrSuccess = 0;
    internal const int CrBufferSmall = 26;
    internal const uint OpenExisting = 3;
    internal const uint DownloadBoot471Ioctl = 0x8000A000;
    internal const uint DownloadBoot472Ioctl = 0x8000A004;
    internal const uint ResetPipeIoctl = 0x8000A008;
    internal const uint DevicePropertyTypeString = 0x00000012;
    private static readonly DevicePropertyKey DeviceServiceProperty = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        6);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    internal static extern int CM_Get_Device_Interface_List_Size(
        out uint length,
        ref Guid interfaceClassGuid,
        string? deviceId,
        uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    internal static extern int CM_Get_Device_Interface_List(
        ref Guid interfaceClassGuid,
        string? deviceId,
        StringBuilder buffer,
        uint bufferLength,
        uint flags);

    [DllImport(
        "CfgMgr32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(
        out uint deviceNode,
        string deviceId,
        uint flags);

    [DllImport(
        "CfgMgr32.dll",
        EntryPoint = "CM_Get_DevNode_PropertyW",
        CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_PropertyW(
        uint deviceNode,
        ref DevicePropertyKey propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint flags);

    internal static bool HasRockUsbService(string deviceId) {
        var status = CM_Locate_DevNodeW(
            out var deviceNode,
            deviceId,
            0);
        if (status != CrSuccess) {
            return false;
        }

        var propertyKey = DeviceServiceProperty;
        uint propertySize = 0;
        status = CM_Get_DevNode_PropertyW(
            deviceNode,
            ref propertyKey,
            out _,
            null,
            ref propertySize,
            0);
        if (status is not (CrSuccess or CrBufferSmall) ||
            propertySize < sizeof(char) ||
            propertySize > 4096) {
            return false;
        }

        var buffer = new byte[propertySize];
        status = CM_Get_DevNode_PropertyW(
            deviceNode,
            ref propertyKey,
            out var propertyType,
            buffer,
            ref propertySize,
            0);
        if (status != CrSuccess ||
            propertyType != DevicePropertyTypeString ||
            propertySize < sizeof(char)) {
            return false;
        }

        var service = Encoding.Unicode.GetString(
                buffer,
                0,
                checked((int)propertySize))
            .TrimEnd('\0');
        return service.Equals(
            "Rockusb",
            StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileA", CharSet = CharSet.Ansi, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string path,
        uint access,
        uint share,
        IntPtr security,
        uint creation,
        uint flags,
        IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint code,
        byte[]? input,
        uint inputLength,
        byte[]? output,
        uint outputLength,
        out uint returned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "ReadFile", SetLastError = true)]
    internal static extern bool ReadFile(
        SafeFileHandle file,
        IntPtr buffer,
        uint count,
        out uint read,
        IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
    internal static extern bool WriteFile(
        SafeFileHandle file,
        IntPtr buffer,
        uint count,
        out uint written,
        IntPtr overlapped);
}
