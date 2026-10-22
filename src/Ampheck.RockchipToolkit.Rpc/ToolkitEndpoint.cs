namespace Ampheck.RockchipToolkit.Rpc;

public sealed record ToolkitEndpoint(string Host, int Port, string Path = "/arkt") {
    public Uri Uri => new UriBuilder("ws", Host, Port, Path).Uri;
    public string HostPort =>
        Host.Contains(':')
            ? $"[{Host}]:{Port}"
            : $"{Host}:{Port}";

    public static ToolkitEndpoint Parse(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.Contains("://", StringComparison.Ordinal)) {
            value = value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
                ? $"ws://{value["tcp:".Length..]}/arkt"
                : $"ws://{value}/arkt";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != "ws" ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.Port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentException($"Invalid toolkit WebSocket endpoint '{value}'.");
        }

        return new ToolkitEndpoint(
            uri.Host,
            uri.Port,
            string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/arkt" : uri.AbsolutePath);
    }

    public override string ToString() => HostPort;
}
