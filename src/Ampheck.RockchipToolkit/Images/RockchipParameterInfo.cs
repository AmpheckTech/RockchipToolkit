using System.Globalization;

namespace Ampheck.RockchipToolkit.Images;

internal sealed record RockchipParameterInfo(
    uint FirmwareVersion,
    string MachineModel,
    string MachineId,
    string Manufacturer,
    IReadOnlyList<RockchipPartition> Partitions) {
    public static RockchipParameterInfo Parse(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            throw new InvalidDataException(
                "The parameter file is empty.");
        }

        var partitions = RockchipParameterParser.Parse(text);
        return new RockchipParameterInfo(
            ParseVersion(ReadValue(text, "FIRMWARE_VER")),
            ReadValue(text, "MACHINE_MODEL"),
            ReadValue(text, "MACHINE_ID"),
            ReadValue(text, "MANUFACTURER"),
            partitions);
    }

    private static uint ParseVersion(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return 0;
        }

        var parts = value.Split('.');
        byte major;
        byte minor;
        ushort build;
        if (parts.Length > 3 ||
            !byte.TryParse(
                parts[0].AsSpan(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out major)) {
            throw InvalidVersion(value);
        }

        minor = 0;
        build = 0;
        if (parts.Length > 1 &&
            !byte.TryParse(
                parts[1].AsSpan(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out minor)) {
            throw InvalidVersion(value);
        }

        if (parts.Length > 2 &&
            !ushort.TryParse(
                parts[2].AsSpan(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out build)) {
            throw InvalidVersion(value);
        }

        return ((uint)major << 24) |
            ((uint)minor << 16) |
            (uint)build;
    }

    private static InvalidDataException InvalidVersion(string value) {
        return new InvalidDataException(
            $"Invalid FIRMWARE_VER value '{value}'. Expected major.minor[.build].");
    }

    private static string ReadValue(string text, string name) {
        foreach (var line in text.Split('\n')) {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator > 0 &&
                trimmed.AsSpan(0, separator).Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase)) {
                return trimmed[(separator + 1)..].Trim();
            }
        }

        return string.Empty;
    }
}
