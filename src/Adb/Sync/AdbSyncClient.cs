namespace Ampheck.RockchipToolkit.Adb.Sync;

public static partial class AdbSyncClient {
    private const int TransferBufferSize = 64 * 1024;
    private static readonly byte[] Send = "SEND"u8.ToArray();
    private static readonly byte[] Receive = "RECV"u8.ToArray();
    private static readonly byte[] Data = "DATA"u8.ToArray();
    private static readonly byte[] Done = "DONE"u8.ToArray();
    private static readonly byte[] Okay = "OKAY"u8.ToArray();
    private static readonly byte[] Fail = "FAIL"u8.ToArray();
}
