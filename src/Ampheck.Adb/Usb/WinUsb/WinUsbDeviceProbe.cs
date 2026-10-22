using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Ampheck.Adb.Usb.WinUsb;

internal static class WinUsbDeviceProbe {
    private const int MaximumAssociatedInterfaces = 32;

    public static WinUsbAdbDevice? TryOpen(
        string path,
        ILogger? logger = null) {
        var deviceHandle = WinUsbNative.CreateFile(
            path,
            WinUsbNative.GenericRead | WinUsbNative.GenericWrite,
            WinUsbNative.ShareRead | WinUsbNative.ShareWrite,
            IntPtr.Zero,
            WinUsbNative.OpenExisting,
            WinUsbNative.FileAttributeNormal |
            WinUsbNative.FileFlagOverlapped,
            IntPtr.Zero);
        if (deviceHandle.IsInvalid) {
            logger?.LogTrace(
                "WinUSB CreateFile failed for {DevicePath} (Win32 {Error})",
                path,
                Marshal.GetLastWin32Error());
            deviceHandle.Dispose();
            return null;
        }

        if (!WinUsbNative.WinUsb_Initialize(
                deviceHandle,
                out var primaryHandleValue)) {
            logger?.LogTrace(
                "WinUSB initialization failed for {DevicePath} " +
                "(Win32 {Error})",
                path,
                Marshal.GetLastWin32Error());
            deviceHandle.Dispose();
            return null;
        }

        var primaryHandle = new SafeWinUsbHandle(primaryHandleValue);
        SafeWinUsbHandle? associatedHandle = null;
        try {
            var interfaceHandle = primaryHandle;
            if (!TryQueryInterface(
                    primaryHandle,
                    out var interfaceDescriptor)) {
                logger?.LogTrace(
                    "WinUSB interface query failed for {DevicePath} " +
                    "(Win32 {Error})",
                    path,
                    Marshal.GetLastWin32Error());
                primaryHandle.Dispose();
                deviceHandle.Dispose();
                return null;
            }

            if (!IsAdbInterface(interfaceDescriptor)) {
                if (!TryFindAssociatedAdbInterface(
                        primaryHandle,
                        out associatedHandle,
                        out interfaceDescriptor)) {
                    logger?.LogTrace(
                        "No ADB interface was found in {DevicePath}",
                        path);
                    primaryHandle.Dispose();
                    deviceHandle.Dispose();
                    return null;
                }

                interfaceHandle = associatedHandle ??
                    throw new IOException(
                        "The ADB interface handle was not created.");
            }

            if (!TryFindBulkPipes(
                    interfaceHandle,
                    interfaceDescriptor,
                    out var bulkInPipe,
                    out var bulkOutPipe)) {
                logger?.LogTrace(
                    "The ADB interface in {DevicePath} has no bulk pipes",
                    path);
                associatedHandle?.Dispose();
                primaryHandle.Dispose();
                deviceHandle.Dispose();
                return null;
            }

            if (!TryReadDeviceIdentity(
                    interfaceHandle,
                    path,
                    out var vendorId,
                    out var productId,
                    out var serialNumber)) {
                logger?.LogTrace(
                    "Unable to read WinUSB device identity from {DevicePath}",
                    path);
                associatedHandle?.Dispose();
                primaryHandle.Dispose();
                deviceHandle.Dispose();
                return null;
            }

            return new WinUsbAdbDevice(
                deviceHandle,
                primaryHandle,
                associatedHandle,
                bulkInPipe,
                bulkOutPipe,
                vendorId,
                productId,
                serialNumber);
        } catch {
            associatedHandle?.Dispose();
            primaryHandle.Dispose();
            deviceHandle.Dispose();
            throw;
        }
    }

    private static bool TryFindAssociatedAdbInterface(
        SafeWinUsbHandle primaryHandle,
        out SafeWinUsbHandle? associatedHandle,
        out WinUsbInterfaceDescriptor descriptor) {
        for (var index = 0;
             index < MaximumAssociatedInterfaces;
             index++) {
            if (!WinUsbNative.WinUsb_GetAssociatedInterface(
                    primaryHandle.DangerousGetHandle(),
                    checked((byte)index),
                    out var associatedHandleValue)) {
                break;
            }

            var candidate = new SafeWinUsbHandle(associatedHandleValue);
            if (TryQueryInterface(candidate, out descriptor) &&
                IsAdbInterface(descriptor)) {
                associatedHandle = candidate;
                return true;
            }

            candidate.Dispose();
        }

        associatedHandle = null;
        descriptor = default;
        return false;
    }

    private static bool TryQueryInterface(
        SafeWinUsbHandle handle,
        out WinUsbInterfaceDescriptor descriptor) {
        return WinUsbNative.WinUsb_QueryInterfaceSettings(
            handle.DangerousGetHandle(),
            0,
            out descriptor);
    }

    private static bool IsAdbInterface(
        WinUsbInterfaceDescriptor descriptor) {
        return AdbUsbInterfaceProfile.Matches(
            descriptor.InterfaceClass,
            descriptor.InterfaceSubClass,
            descriptor.InterfaceProtocol);
    }

    private static bool TryFindBulkPipes(
        SafeWinUsbHandle interfaceHandle,
        WinUsbInterfaceDescriptor descriptor,
        out byte bulkInPipe,
        out byte bulkOutPipe) {
        bulkInPipe = 0;
        bulkOutPipe = 0;
        for (var index = 0; index < descriptor.EndpointCount; index++) {
            if (!WinUsbNative.WinUsb_QueryPipe(
                    interfaceHandle.DangerousGetHandle(),
                    descriptor.AlternateSetting,
                    checked((byte)index),
                    out var pipe) ||
                pipe.PipeType != WinUsbPipeType.Bulk) {
                continue;
            }

            if ((pipe.PipeId & 0x80) != 0) {
                bulkInPipe = pipe.PipeId;
            } else {
                bulkOutPipe = pipe.PipeId;
            }
        }

        return bulkInPipe != 0 && bulkOutPipe != 0;
    }

    private static bool TryReadDeviceIdentity(
        SafeWinUsbHandle interfaceHandle,
        string path,
        out ushort vendorId,
        out ushort productId,
        out string? serialNumber) {
        vendorId = 0;
        productId = 0;
        serialNumber = null;
        var descriptor = new byte[18];
        if (!WinUsbNative.WinUsb_GetDescriptor(
                interfaceHandle.DangerousGetHandle(),
                WinUsbNative.DeviceDescriptorType,
                0,
                0,
                descriptor,
                checked((uint)descriptor.Length),
                out var transferred) ||
            transferred < descriptor.Length) {
            if (!TryParseIdentityFromPath(
                    path,
                    out vendorId,
                    out productId)) {
                return false;
            }

            serialNumber = GetFallbackSerial(path);
            return true;
        }

        vendorId = BinaryPrimitives.ReadUInt16LittleEndian(
            descriptor.AsSpan(8));
        productId = BinaryPrimitives.ReadUInt16LittleEndian(
            descriptor.AsSpan(10));
        var serialIndex = descriptor[16];
        serialNumber = serialIndex == 0
            ? GetFallbackSerial(path)
            : TryReadSerialNumber(
                interfaceHandle,
                serialIndex) ??
                GetFallbackSerial(path);
        return vendorId != 0 && productId != 0;
    }

    private static string? TryReadSerialNumber(
        SafeWinUsbHandle interfaceHandle,
        byte serialIndex) {
        var languageDescriptor = new byte[4];
        if (!WinUsbNative.WinUsb_GetDescriptor(
                interfaceHandle.DangerousGetHandle(),
                WinUsbNative.StringDescriptorType,
                0,
                0,
                languageDescriptor,
                checked((uint)languageDescriptor.Length),
                out var languageTransferred) ||
            languageTransferred < languageDescriptor.Length) {
            return null;
        }

        var languageId = BinaryPrimitives.ReadUInt16LittleEndian(
            languageDescriptor.AsSpan(2));
        var descriptor = new byte[255];
        if (!WinUsbNative.WinUsb_GetDescriptor(
                interfaceHandle.DangerousGetHandle(),
                WinUsbNative.StringDescriptorType,
                serialIndex,
                languageId,
                descriptor,
                checked((uint)descriptor.Length),
                out var transferred) ||
            transferred < 2) {
            return null;
        }

        var length = Math.Min(descriptor[0], transferred);
        if (length <= 2) {
            return null;
        }

        return Encoding.Unicode.GetString(
                descriptor,
                2,
                checked((int)length - 2))
            .TrimEnd('\0');
    }

    private static bool TryParseIdentityFromPath(
        string path,
        out ushort vendorId,
        out ushort productId) {
        vendorId = 0;
        productId = 0;
        var vendorIndex = path.IndexOf(
            "VID_",
            StringComparison.OrdinalIgnoreCase);
        var productIndex = path.IndexOf(
            "&PID_",
            StringComparison.OrdinalIgnoreCase);
        return vendorIndex >= 0 &&
            vendorIndex + 8 <= path.Length &&
            productIndex > vendorIndex &&
            productIndex + 9 <= path.Length &&
            ushort.TryParse(
                path.AsSpan(vendorIndex + 4, 4),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out vendorId) &&
            ushort.TryParse(
                path.AsSpan(productIndex + 5, 4),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out productId);
    }

    private static string? GetFallbackSerial(string path) {
        var parts = path.Split(
            '#',
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3
            ? $"usb:{parts[2]}"
            : null;
    }
}
