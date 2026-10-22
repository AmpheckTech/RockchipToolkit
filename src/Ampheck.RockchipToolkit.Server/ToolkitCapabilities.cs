namespace Ampheck.RockchipToolkit.Server;

internal static class ToolkitCapabilities {
    public static IReadOnlyList<string> All { get; } = [
        "multi_channel",
        "attachments",
        "downloads",
        "rockusb",
        "firmware_zip",
        "raw_region",
        "serial",
        "serial_history",
        "adb",
        "bounded_uploads",
        "bounded_extraction",
        "mdns"
    ];
}
