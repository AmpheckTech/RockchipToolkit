namespace Ampheck.Adb.Protocol;

internal readonly record struct AdbMessage(
    AdbCommand Command,
    uint Arg0,
    uint Arg1,
    byte[] Data);
