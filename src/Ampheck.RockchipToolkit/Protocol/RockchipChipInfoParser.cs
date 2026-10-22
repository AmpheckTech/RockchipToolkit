using System.Text;

namespace Ampheck.RockchipToolkit.Protocol;

public static class RockchipChipInfoParser {
    private const int IdentifierLength = 4;

    public static RockchipChipInfo Parse(ReadOnlySpan<byte> data) {
        if (data.Length < IdentifierLength) {
            throw new InvalidDataException(
                "The chip-info response is shorter than four bytes.");
        }

        Span<byte> identifierBytes = stackalloc byte[IdentifierLength];
        data[..IdentifierLength].CopyTo(identifierBytes);
        identifierBytes.Reverse();
        var token = Encoding.ASCII.GetString(identifierBytes)
            .TrimEnd('\0');
        var identifier = IsPrintableIdentifier(token)
            ? token.StartsWith("RK", StringComparison.OrdinalIgnoreCase)
                ? token.ToUpperInvariant()
                : $"RK{token}"
            : "Unknown";

        return new RockchipChipInfo(
            identifier,
            GetPlatformName(identifier),
            Convert.ToHexString(data));
    }

    private static bool IsPrintableIdentifier(string value) {
        return value.Length > 0 &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character));
    }

    private static string GetPlatformName(string identifier) {
        return identifier switch {
            "RK3568" => "RK3566/RK3568",
            _ => identifier
        };
    }
}
