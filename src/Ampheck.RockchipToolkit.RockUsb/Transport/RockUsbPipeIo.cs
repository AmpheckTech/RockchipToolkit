using System.ComponentModel;
using System.Runtime.InteropServices;
using Ampheck.RockchipToolkit.RockUsb.Configuration;
using Ampheck.RockchipToolkit.RockUsb.Interop;
using Microsoft.Win32.SafeHandles;

namespace Ampheck.RockchipToolkit.RockUsb.Transport;

internal static class RockUsbPipeIo {
    public static SafeFileHandle OpenFile(
        string path,
        RockUsbOptions options) {
        return RockUsbNative.CreateFile(
            path,
            options.ControlDeviceAccess,
            options.ControlDeviceShareMode,
            IntPtr.Zero,
            RockUsbNative.OpenExisting,
            0,
            IntPtr.Zero);
    }

    public static SafeFileHandle OpenPipe(
        string path,
        string suffix,
        uint access,
        uint share) {
        return RockUsbNative.CreateFile(
            path + suffix,
            access,
            share,
            IntPtr.Zero,
            RockUsbNative.OpenExisting,
            0,
            IntPtr.Zero);
    }

    public static void Reset(
        string path,
        RockUsbOptions options,
        byte pipe) {
        using var handle = OpenFile(path, options);
        if (handle.IsInvalid) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to open the rockusb.sys device for pipe reset.");
        }

        var input = new[] { pipe };
        if (!RockUsbNative.DeviceIoControl(
                handle,
                RockUsbNative.ResetPipeIoctl,
                input,
                1,
                null,
                0,
                out _,
                IntPtr.Zero)) {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                error,
                $"rockusb.sys pipe reset failed (Win32 {error}).");
        }
    }
}
