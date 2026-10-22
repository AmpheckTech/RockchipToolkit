using System.Runtime.InteropServices;
using System.Text;

namespace Ampheck.Adb.Usb.WinUsb;

internal static class WinUsbDeviceEnumerator {
    private static readonly Guid[] InterfaceClasses = [
        WinUsbNative.AndroidAdbInterface,
        WinUsbNative.AndroidUsbInterface,
        WinUsbNative.UsbDeviceInterface
    ];

    public static IReadOnlyList<string> Enumerate(
        CancellationToken cancellationToken) {
        var paths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        AddSetupApiPaths(paths);
        foreach (var interfaceClass in InterfaceClasses) {
            AddInterfacePaths(
                interfaceClass,
                paths,
                cancellationToken);
        }

        return paths.ToArray();
    }

    private static void AddSetupApiPaths(HashSet<string> paths) {
        var deviceInfoSet = SetupApiNative.SetupDiGetClassDevs(
            IntPtr.Zero,
            "USB",
            IntPtr.Zero,
            SetupApiNative.Present |
            SetupApiNative.AllClasses);
        if (deviceInfoSet == new IntPtr(-1)) {
            return;
        }

        try {
            for (uint index = 0; ; index++) {
                var deviceInfo = new SetupDeviceInfoData {
                    Size = checked((uint)Marshal.SizeOf<
                        SetupDeviceInfoData>())
                };
                if (!SetupApiNative.SetupDiEnumDeviceInfo(
                        deviceInfoSet,
                        index,
                        ref deviceInfo)) {
                    break;
                }

                var instanceId = new StringBuilder(1024);
                if (!SetupApiNative.SetupDiGetDeviceInstanceId(
                        deviceInfoSet,
                        ref deviceInfo,
                        instanceId,
                        checked((uint)instanceId.Capacity),
                        out _)) {
                    continue;
                }

                var id = instanceId.ToString();
                if (!id.Contains(
                        "VID_",
                        StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                var pathPrefix = @"\\?\" + id.Replace('\\', '#');
                foreach (var interfaceClass in InterfaceClasses) {
                    paths.Add(
                        $"{pathPrefix}#{interfaceClass:B}");
                }
            }
        } finally {
            SetupApiNative.SetupDiDestroyDeviceInfoList(
                deviceInfoSet);
        }
    }

    private static void AddInterfacePaths(
        Guid interfaceClass,
        HashSet<string> paths,
        CancellationToken cancellationToken) {
        var interfaceGuid = interfaceClass;
        var status = WinUsbNative.CM_Get_Device_Interface_List_Size(
            out var length,
            ref interfaceGuid,
            null,
            WinUsbNative.PresentDeviceInterfaces);
        if (status != 0 || length == 0) {
            return;
        }

        var buffer = new StringBuilder(checked((int)length));
        status = WinUsbNative.CM_Get_Device_Interface_List(
            ref interfaceGuid,
            null,
            buffer,
            length,
            WinUsbNative.PresentDeviceInterfaces);
        if (status != 0) {
            return;
        }

        foreach (var path in buffer
                     .ToString()
                     .Split('\0', StringSplitOptions.RemoveEmptyEntries)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (path.Contains(
                    "VID_",
                    StringComparison.OrdinalIgnoreCase)) {
                paths.Add(path.Trim());
            }
        }
    }
}
