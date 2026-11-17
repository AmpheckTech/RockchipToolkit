using System.Globalization;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Protocol.RockUsb;
using Ampheck.RockchipToolkit.Transport.Serial;

namespace Ampheck.RockchipToolkit.Cli.Commands.Parsers;

internal static class ValueParser {
    public static ushort ParseUInt16(string option, string value) {
        if (!ushort.TryParse(
                GetDigits(value),
                GetStyle(value),
                CultureInfo.InvariantCulture,
                out var result)) {
            throw InvalidValue(option, "an unsigned 16-bit integer");
        }

        return result;
    }

    public static uint ParseUInt32(string option, string value) {
        if (!uint.TryParse(
                GetDigits(value),
                GetStyle(value),
                CultureInfo.InvariantCulture,
                out var result)) {
            throw InvalidValue(option, "an unsigned 32-bit integer");
        }

        return result;
    }

    public static ResetMode ParseResetMode(string name, string value) =>
        value.ToLowerInvariant() switch {
            "normal" => ResetMode.Normal,
            "reset-mass-storage" => ResetMode.ResetMassStorage,
            "power-off" => ResetMode.PowerOff,
            "reset-mask-rom" => ResetMode.ResetMaskRom,
            "disconnect" => ResetMode.Disconnect,
            _ => throw new ArgumentException(
                $"{name} must be disconnect, normal, power-off, " +
                "reset-mask-rom, or reset-mass-storage.")
        };

    public static StorageType ParseStorageType(string name, string value) =>
        value.ToLowerInvariant() switch {
            "emmc" => StorageType.Emmc,
            "sd-card" => StorageType.SdCard,
            "spi-nor" => StorageType.SpiNor,
            _ => throw new ArgumentException(
                $"{name} must be emmc, sd-card, or spi-nor.")
        };

    public static ToolkitTransport ParseTransport(string name, string value) =>
        value.ToLowerInvariant() switch {
            "auto" => ToolkitTransport.Auto,
            "rockusb" => ToolkitTransport.RockUsb,
            "libusb" => ToolkitTransport.LibUsb,
            _ => throw new ArgumentException(
                $"{name} must be auto, rockusb, or libusb.")
        };

    public static int ParseDataBits(string name, string value) {
        if (!int.TryParse(value, out var dataBits) ||
            dataBits is < 5 or > 8) {
            throw new ArgumentException(
                $"{name} must be 5, 6, 7, or 8.");
        }

        return dataBits;
    }

    public static SerialParity ParseParity(string name, string value) =>
        value.ToLowerInvariant() switch {
            "odd" => SerialParity.Odd,
            "even" => SerialParity.Even,
            "mark" => SerialParity.Mark,
            "none" => SerialParity.None,
            "space" => SerialParity.Space,
            _ => throw new ArgumentException(
                $"{name} must be none, odd, even, mark, or space.")
        };

    public static SerialStopBits ParseStopBits(string name, string value) =>
        value.ToLowerInvariant() switch {
            "one-point-five" => SerialStopBits.OnePointFive,
            "one" => SerialStopBits.One,
            "two" => SerialStopBits.Two,
            _ => throw new ArgumentException(
                $"{name} must be one, one-point-five, or two.")
        };

    public static SerialHandshake ParseHandshake(string name, string value) =>
        value.ToLowerInvariant() switch {
            "xon-xoff" => SerialHandshake.XOnXOff,
            "rts-cts" => SerialHandshake.RequestToSend,
            "rts-cts-xon-xoff" =>
                SerialHandshake.RequestToSendXOnXOff,
            "none" => SerialHandshake.None,
            _ => throw new ArgumentException(
                $"{name} must be none, xon-xoff, rts-cts, " +
                "or rts-cts-xon-xoff.")
        };

    public static long? ParseOptionalInt64(
        string option,
        string? value
    ) {
        if (value is null) {
            return null;
        }

        if (!long.TryParse(
                GetDigits(value),
                GetStyle(value),
                CultureInfo.InvariantCulture,
                out var result) ||
            result < 0) {
            throw InvalidValue(
                option,
                "a non-negative 64-bit integer");
        }

        return result;
    }

    private static string GetDigits(string value) {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;
    }

    private static NumberStyles GetStyle(string value) {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? NumberStyles.HexNumber
            : NumberStyles.Integer;
    }

    private static ArgumentException InvalidValue(string option, string expected) {
        return new ArgumentException(
            $"{option} expects {expected} (decimal or 0x-prefixed hex).");
    }
}
