using System.Globalization;
using Ampheck.RockchipToolkit.Features.Devices.Model;

namespace Ampheck.RockchipToolkit.Arkt.Terminal;

internal static class OutputFormatter {
    private static readonly (int Bit, string Label)[] CapabilityBits = [
        (0, "Direct LBA"),
        (1, "Vendor storage API"),
        (2, "First 4 MiB access"),
        (3, "Read LBA"),
        (4, "New vendor storage API"),
        (5, "Read UART data from RAM"),
        (6, "Read IDB configuration"),
        (7, "Read secure mode"),
        (8, "New IDB"),
        (9, "Storage media information"),
        (10, "LBA write parity"),
        (11, "Read OTP data"),
        (12, "USB 3 download"),
        (13, "Write OTP proof"),
        (14, "Write cipher key")
    ];
    public static string FormatDevices(
        DeviceListResult result
    ) {
        return string.Join(
            Environment.NewLine,
            result.Devices.Select(device =>
                $"{device.VendorId:X4}:{device.ProductId:X4} " +
                $"{device.Mode} USB {device.UsbVersion:X4} " +
                $"{device.PlatformId ?? string.Empty} " +
                $"{device.Description ?? string.Empty} " +
                $"{device.SerialNumber ?? string.Empty}".TrimEnd()));
    }

    public static string FormatInfo(DeviceInfoResult info) {
        return string.Join(
            Environment.NewLine,
            "Chip information:",
            $"  Chip code:  {info.ChipIdentifier}",
            $"  Platform:   {info.PlatformName}",
            string.Empty,
            "Flash information:",
            $"  Manufacturer: {info.Manufacturer} " +
            $"(0x{info.ManufacturerCode:X2})",
            $"  Capacity:     {FormatBytes(info.CapacityBytes)}",
            $"  Block size:   {FormatBytes(info.BlockSizeBytes)}",
            $"  Page size:    {FormatBytes(info.PageSizeBytes)}",
            $"  Blocks:       {info.BlockCount:N0}",
            $"  ECC bits:     {info.EccBits}",
            $"  Access time:  {info.AccessTime}",
            $"  Flash CS:     {info.ChipSelectMask}");
    }

    public static string FormatChipInfo(
        DeviceChipInfoResult info
    ) {
        return string.Join(
            Environment.NewLine,
            "Chip information:",
            $"  Chip code:  {info.Identifier}",
            $"  Platform:   {info.PlatformName}");
    }

    public static string FormatCapabilities(
        DeviceCapabilityResult result
    ) {
        return string.Join(
            Environment.NewLine,
            [
                "Capability information:",
                $"  Flags:       0x{result.Flags:X16}",
                .. CapabilityBits.Select(item =>
                    $"  {item.Label}: " +
                    ((result.Flags & (1UL << item.Bit)) != 0
                        ? "enabled"
                        : "disabled"))
            ]);
    }

    private static string FormatBytes(long bytes) {
        if (bytes >= 1L << 30) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1L << 30):F2} GiB");
        }

        if (bytes >= 1L << 20) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1L << 20):F0} MiB");
        }

        if (bytes >= 1L << 10) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1L << 10):F0} KiB");
        }

        return $"{bytes} bytes";
    }
}
