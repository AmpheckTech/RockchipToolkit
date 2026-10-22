namespace Ampheck.Adb;

public sealed record AdbInstallOptions {
    public bool ReplaceExisting { get; init; } = true;
    public bool AllowDowngrade { get; init; }
    public bool GrantPermissions { get; init; }
    public int? UserId { get; init; }
}
