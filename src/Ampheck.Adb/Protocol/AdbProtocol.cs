using System.Buffers.Binary;
using Ampheck.Adb.Usb;

namespace Ampheck.Adb.Protocol;

internal static class AdbProtocol {
    private const int HeaderLength = 24;
    private const uint Version = 0x01000000;

    public static async ValueTask SendAsync(
        IAdbTransport transport,
        AdbCommand command,
        uint arg0,
        uint arg1,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) {
        var header = new byte[HeaderLength];
        var commandValue = (uint)command;
        BinaryPrimitives.WriteUInt32LittleEndian(
            header,
            commandValue);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(4),
            arg0);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(8),
            arg1);
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(12),
            checked((uint)data.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(16),
            CalculateChecksum(data.Span));
        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(20),
            commandValue ^ uint.MaxValue);
        await transport.WriteAsync(header, cancellationToken)
            .ConfigureAwait(false);
        if (!data.IsEmpty) {
            await transport.WriteAsync(data, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public static async ValueTask<AdbMessage> ReadAsync(
        IAdbTransport transport,
        uint maximumPayload,
        CancellationToken cancellationToken,
        TimeSpan? readTimeout = null) {
        var header = new byte[HeaderLength];
        await ReadExactlyAsync(
                transport,
                header,
                cancellationToken,
                readTimeout)
            .ConfigureAwait(false);
        var commandValue = BinaryPrimitives.ReadUInt32LittleEndian(header);
        var arg0 = BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(4));
        var arg1 = BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(8));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(12));
        var magic = BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(20));
        if (magic != (commandValue ^ uint.MaxValue) ||
            length > maximumPayload ||
            !Enum.IsDefined((AdbCommand)commandValue)) {
            throw new InvalidDataException(
                "The ADB device returned an invalid message header.");
        }

        var data = new byte[checked((int)length)];
        await ReadExactlyAsync(
                transport,
                data,
                cancellationToken,
                readTimeout)
            .ConfigureAwait(false);
        return new AdbMessage(
            (AdbCommand)commandValue,
            arg0,
            arg1,
            data);
    }

    public static byte[] ConnectPayload() {
        return System.Text.Encoding.ASCII.GetBytes(
            "host::features=shell_v2,cmd,stat_v2\0");
    }

    public static uint VersionValue => Version;

    private static uint CalculateChecksum(ReadOnlySpan<byte> data) {
        uint checksum = 0;
        foreach (var value in data) {
            checksum += value;
        }

        return checksum;
    }

    private static async ValueTask ReadExactlyAsync(
        IAdbTransport transport,
        Memory<byte> buffer,
        CancellationToken cancellationToken,
        TimeSpan? readTimeout) {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await transport.ReadAsync(
                    buffer[offset..],
                    readTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0) {
                throw new EndOfStreamException(
                    "The ADB device closed the USB stream.");
            }

            offset += read;
        }
    }
}
