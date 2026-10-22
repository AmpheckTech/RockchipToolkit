using System.ComponentModel;
using System.Diagnostics;
using Ampheck.RockchipToolkit.RockUsb.Configuration;
using Ampheck.RockchipToolkit.RockUsb.Interop;
using Ampheck.RockchipToolkit.Transport;

namespace Ampheck.RockchipToolkit.RockUsb.Device;

internal static class RockUsbDeviceEnumerator {
    private static readonly Guid UsbDeviceInterface =
        new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    private const string UsbInterfaceSuffix =
        "#{A5DCBF10-6530-11D2-901F-00C04FB951ED}";

    public static IReadOnlyList<UsbDeviceInfo> Enumerate(
        RockUsbOptions options,
        CancellationToken cancellationToken) {
        var devices = new List<UsbDeviceInfo>();
        EnumerateConfigurationManager(
            devices,
            options,
            cancellationToken);
        AddPnPUtilDevices(
            devices,
            options,
            cancellationToken);
        return devices.AsReadOnly();
    }

    private static void EnumerateConfigurationManager(
        List<UsbDeviceInfo> devices,
        RockUsbOptions options,
        CancellationToken cancellationToken) {
        try {
            var interfaceGuid = UsbDeviceInterface;
            var status = RockUsbNative.CM_Get_Device_Interface_List_Size(
                out var length,
                ref interfaceGuid,
                null,
                RockUsbNative.Present);
            if (status != RockUsbNative.CrSuccess || length == 0) {
                return;
            }

            var buffer = new System.Text.StringBuilder(checked((int)length));
            status = RockUsbNative.CM_Get_Device_Interface_List(
                ref interfaceGuid,
                null,
                buffer,
                length,
                RockUsbNative.Present);
            if (status != RockUsbNative.CrSuccess) {
                return;
            }

            foreach (var path in buffer
                         .ToString()
                         .Split('\0', StringSplitOptions.RemoveEmptyEntries)) {
                cancellationToken.ThrowIfCancellationRequested();
                TryAddDevice(devices, path, options);
            }
        } catch (Win32Exception) {
        }
    }

    private static void AddPnPUtilDevices(
        List<UsbDeviceInfo> devices,
        RockUsbOptions options,
        CancellationToken cancellationToken) {
        if (devices.Any(device =>
                (!options.VendorId.HasValue ||
                    device.VendorId == options.VendorId) &&
                (!options.ProductId.HasValue ||
                    device.ProductId == options.ProductId))) {
            return;
        }

        Process? process;
        try {
            process = Process.Start(CreatePnPUtilStartInfo());
        } catch (Win32Exception) {
            return;
        }

        if (process is null) {
            return;
        }

        using var ownedProcess = process;
        var outputTask = process.StandardOutput.ReadToEndAsync(
            CancellationToken.None);
        if (!process.WaitForExit(5000)) {
            TryKill(process);
            return;
        }

        var output = outputTask.GetAwaiter().GetResult();
        foreach (var line in output.Split('\n')) {
            cancellationToken.ThrowIfCancellationRequested();
            var start = line.IndexOf(
                @"\\?\USB#",
                StringComparison.OrdinalIgnoreCase);
            if (start >= 0) {
                TryAddDevice(devices, line[start..].Trim(), options);
            }
        }
    }

    private static ProcessStartInfo CreatePnPUtilStartInfo() {
        var executable = OperatingSystem.IsWindows() &&
            Environment.Is64BitOperatingSystem &&
            !Environment.Is64BitProcess
                ? Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows),
                    "Sysnative",
                    "pnputil.exe")
                : Path.Combine(
                    Environment.SystemDirectory,
                    "pnputil.exe");
        return new ProcessStartInfo {
            FileName = executable,
            Arguments = "/enum-devices /connected /interfaces",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
    }

    private static void TryAddDevice(
        List<UsbDeviceInfo> devices,
        string path,
        RockUsbOptions options) {
        if (!path.EndsWith(
                UsbInterfaceSuffix,
                StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (!TryGetDeviceInstanceId(path, out var deviceId) ||
            !RockUsbNative.HasRockUsbService(deviceId)) {
            return;
        }

        if (!TryParseRockchipPath(
                path,
                out var vendorId,
                out var productId)) {
            return;
        }

        if (options.VendorId is { } wantedVendorId &&
            vendorId != wantedVendorId) {
            return;
        }

        if (options.ProductId is { } wantedProductId &&
            productId != wantedProductId) {
            return;
        }

        if (devices.Any(device =>
                string.Equals(
                    device.PlatformId,
                    path,
                    StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        devices.Add(new UsbDeviceInfo(
            vendorId,
            productId,
            0,
            DetectMode(path, vendorId, productId),
            PlatformId: path));
    }

    private static bool TryGetDeviceInstanceId(
        string path,
        out string deviceId) {
        const string prefix = @"\\?\";
        var interfaceIndex = path.LastIndexOf(
            "#{",
            StringComparison.Ordinal);
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            interfaceIndex <= prefix.Length) {
            deviceId = string.Empty;
            return false;
        }

        deviceId = path[prefix.Length..interfaceIndex].Replace('#', '\\');
        return deviceId.StartsWith(
            @"USB\",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRockchipPath(
        string? path,
        out ushort vendorId,
        out ushort productId) {
        vendorId = 0;
        productId = 0;
        if (path is null) {
            return false;
        }

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

    private static RockchipUsbMode DetectMode(
        string path,
        ushort vendorId,
        ushort productId) {
        if (!RockchipUsbCatalog.IsRockUsbDevice(
                vendorId,
                productId)) {
            return RockchipUsbMode.None;
        }

        var interfaceSeparator = path.LastIndexOf(
            "#{",
            StringComparison.Ordinal);
        var instanceSeparator = interfaceSeparator < 0
            ? -1
            : path.LastIndexOf(
                '#',
                interfaceSeparator - 1);
        var instance = instanceSeparator < 0
            ? path
            : path[(instanceSeparator + 1)..interfaceSeparator];

        return instance.Contains('&')
            ? RockchipUsbMode.MaskRom
            : RockchipUsbMode.Loader;
    }

    private static void TryKill(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (Exception error) when (
            error is InvalidOperationException or Win32Exception) {
        }
    }
}
