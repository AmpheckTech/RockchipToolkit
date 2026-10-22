namespace Ampheck.Adb.Sync;

public sealed record AdbFileInfo(
    string Name,
    uint Mode,
    long Size,
    DateTimeOffset LastWriteTime) {
    public bool IsDirectory => (Mode & 0x4000) != 0;
    public UnixFileMode Permissions =>
        (UnixFileMode)(Mode & 0x1FF);
}
