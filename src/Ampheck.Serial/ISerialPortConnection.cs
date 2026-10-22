namespace Ampheck.Serial;

internal interface ISerialPortConnection : IDisposable {
    void Open();
    int Read(byte[] buffer, int offset, int count);
    void Write(ReadOnlyMemory<byte> data);
}
