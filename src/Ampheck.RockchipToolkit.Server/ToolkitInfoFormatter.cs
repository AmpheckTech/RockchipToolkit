using System.Globalization;
using Ampheck.RockchipToolkit.Protocol;

namespace Ampheck.RockchipToolkit.Server;

internal static class ToolkitInfoFormatter {
    private static readonly (
        RockchipCapabilityFlags Flag,
        string Label)[] CapabilityFlags = [
            (RockchipCapabilityFlags.DirectLba, "Direct LBA"),
            (RockchipCapabilityFlags.VendorStorage, "Vendor storage API"),
            (RockchipCapabilityFlags.First4MiBAccess, "First 4 MiB access"),
            (RockchipCapabilityFlags.ReadLba, "Read LBA"),
            (RockchipCapabilityFlags.NewVendorStorage, "New vendor storage API"),
            (RockchipCapabilityFlags.ReadUartRam, "Read UART data from RAM"),
            (RockchipCapabilityFlags.ReadIdbConfig, "Read IDB configuration"),
            (RockchipCapabilityFlags.ReadSecureMode, "Read secure mode"),
            (RockchipCapabilityFlags.NewIdb, "New IDB"),
            (RockchipCapabilityFlags.StorageMediaInfo, "Storage media information"),
            (RockchipCapabilityFlags.LbaWriteParity, "LBA write parity"),
            (RockchipCapabilityFlags.ReadOtpData, "Read OTP data"),
            (RockchipCapabilityFlags.Usb3Download, "USB 3 download"),
            (RockchipCapabilityFlags.WriteOtpProof, "Write OTP proof"),
            (RockchipCapabilityFlags.WriteCipherKey, "Write cipher key")
        ];

    public static string FormatChip(RockchipChipInfo info) {
        return string.Join(
            Environment.NewLine,
            "Chip information:",
            $"  Chip code:  {info.Identifier}",
            $"  Platform:   {info.PlatformName}");
    }

    public static string FormatFlash(RockchipFlashInfo info) {
        return string.Join(
            Environment.NewLine,
            "Flash information:",
            $"  Manufacturer: {info.Manufacturer} " +
            $"(0x{info.ManufacturerCode:X2})",
            $"  Capacity:     {FormatBytes(info.CapacityBytes)}",
            $"  Block size:   {FormatBytes(info.BlockSizeBytes)} " +
            $"({FormatNumber(info.SectorsPerBlock)} sectors)",
            $"  Page size:    {FormatBytes(info.PageSizeBytes)}",
            $"  Blocks:       {FormatNumber(info.BlockCount)}",
            $"  ECC bits:     {info.EccBits}",
            $"  Access time:  {info.AccessTime}",
            $"  Flash CS:     {FormatChipSelects(info.ChipSelectMask)}");
    }

    public static string FormatCapabilities(
        RockchipCapabilityInfo info) {
        var lines = new List<string> {
            "Capability information:",
            $"  Flags:       0x{(ulong)info.Flags:X16}"
        };
        lines.AddRange(CapabilityFlags.Select(item =>
            $"  {item.Label}: " +
            (info.Flags.HasFlag(item.Flag)
                ? "enabled"
                : "disabled")));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatBytes(ulong bytes) {
        if (bytes >= 1UL << 30) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1UL << 30):F2} GiB");
        }

        if (bytes >= 1UL << 20) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1UL << 20):F0} MiB");
        }

        if (bytes >= 1UL << 10) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1UL << 10):F0} KiB");
        }

        return $"{bytes} bytes";
    }

    private static string FormatChipSelects(byte mask) {
        if (mask == 0) {
            return "none";
        }

        return string.Join(
            ", ",
            Enumerable.Range(0, 8)
                .Where(index => (mask & (1 << index)) != 0)
                .Select(index => index.ToString(
                    CultureInfo.InvariantCulture)));
    }

    private static string FormatNumber(uint value) {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
