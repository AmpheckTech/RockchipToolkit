using System.Globalization;
using System.Text;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitWorkerEvent {
    private const string Marker = "\u001eARKT";
    private const string EventMarker = "\u001eARKTE";

    public static string Encode(ToolkitEvent value) {
        if (value.Source == "progress" &&
            value.Current is { } current &&
            value.Total is { } total) {
            var label = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value.Message));
            return string.Join(
                '\t',
                Marker,
                label,
                current.ToString(CultureInfo.InvariantCulture),
                total.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(
            '\t',
            EventMarker,
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value.Source)),
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value.Message)));
    }

    public static bool TryParse(
        string line,
        out ToolkitEvent value) {
        var fields = line.Split('\t');
        if (fields.Length == 3 &&
            fields[0] == EventMarker) {
            try {
                value = new ToolkitEvent(
                    Encoding.UTF8.GetString(
                        Convert.FromBase64String(fields[1])),
                    Encoding.UTF8.GetString(
                        Convert.FromBase64String(fields[2])));
                return true;
            } catch (FormatException) {
            }
        }

        if (fields.Length == 4 &&
            fields[0] == Marker &&
            long.TryParse(
                fields[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var current) &&
            long.TryParse(
                fields[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var total)) {
            try {
                value = new ToolkitEvent(
                    "progress",
                    Encoding.UTF8.GetString(
                        Convert.FromBase64String(fields[1])),
                    current,
                    total);
                return true;
            } catch (FormatException) {
            }
        }

        value = null!;
        return false;
    }
}
