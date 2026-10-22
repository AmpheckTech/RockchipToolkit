namespace Ampheck.Adb.Protocol;

internal enum AdbCommand : uint {
    Connect = 0x4E584E43,
    Auth = 0x48545541,
    Open = 0x4E45504F,
    Okay = 0x59414B4F,
    Write = 0x45545257,
    Close = 0x45534C43
}
