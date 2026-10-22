using System.Runtime.InteropServices;
using System.Text;

namespace Ampheck.Adb.Usb.WinUsb;

internal static class SetupApiNative {
    internal const uint Present = 0x00000002;
    internal const uint AllClasses = 0x00000004;

    [DllImport(
        "setupapi.dll",
        EntryPoint = "SetupDiGetClassDevsW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(
        IntPtr classGuid,
        string? enumerator,
        IntPtr parentWindow,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SetupDeviceInfoData deviceInfoData);

    [DllImport(
        "setupapi.dll",
        EntryPoint = "SetupDiGetDeviceInstanceIdW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    internal static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SetupDeviceInfoData deviceInfoData,
        StringBuilder deviceInstanceId,
        uint deviceInstanceIdSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiDestroyDeviceInfoList(
        IntPtr deviceInfoSet);
}
