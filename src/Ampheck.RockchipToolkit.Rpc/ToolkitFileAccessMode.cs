namespace Ampheck.RockchipToolkit.Rpc;

public enum ToolkitFileAccessMode {
    Auto,
    Server,
    Upload
}

public static class ToolkitFileAccess {
    public static ToolkitFileAccessMode Parse(string? value) {
        return value?.Trim().ToLowerInvariant() switch {
            null or "" or "auto" => ToolkitFileAccessMode.Auto,
            "server" or "local" => ToolkitFileAccessMode.Server,
            "upload" or "remote" => ToolkitFileAccessMode.Upload,
            _ => throw new ArgumentException(
                $"Invalid file access mode '{value}'.")
        };
    }
}
