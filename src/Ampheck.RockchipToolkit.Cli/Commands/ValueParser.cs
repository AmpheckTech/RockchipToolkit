using System.Globalization;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class ValueParser {
    public static ushort? ParseOptionalUInt16(
        string option,
        string? value) {
        return value is null
            ? null
            : ParseUInt16(option, value);
    }

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

    public static uint? ParseOptionalUInt32(
        string option,
        string? value) {
        return value is null
            ? null
            : ParseUInt32(option, value);
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

    public static long? ParseOptionalInt64(
        string option,
        string? value) {
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
