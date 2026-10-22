using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

public static class ToolkitBroadcastDiscovery {
    public const int Port = 5071;
    private static ReadOnlySpan<byte> Magic => "ARKD"u8;
    private const byte Version = 1;
    private const byte RequestType = 1;
    private const byte ResponseType = 2;

    public static byte[] EncodeRequest() =>
        [.. Magic, Version, RequestType];

    public static byte[] EncodeResponse(
        string instanceId,
        string hostName,
        int port,
        bool tokenRequired) {
        var guid = Encoding.UTF8.GetBytes(instanceId);
        var host = Encoding.UTF8.GetBytes(hostName);
        if (guid.Length > ushort.MaxValue ||
            host.Length > ushort.MaxValue) {
            throw new InvalidDataException(
                "Discovery identity is too long.");
        }

        var result = new byte[
            13 + guid.Length + host.Length];
        Magic.CopyTo(result);
        result[4] = Version;
        result[5] = ResponseType;
        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(6),
            checked((ushort)port));
        result[8] = tokenRequired ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(9),
            (ushort)guid.Length);
        guid.CopyTo(result, 11);
        var hostLengthOffset = 11 + guid.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(hostLengthOffset),
            (ushort)host.Length);
        host.CopyTo(result, hostLengthOffset + 2);
        return result;
    }

    public static bool IsRequest(ReadOnlySpan<byte> packet) =>
        packet.Length == 6 &&
        packet[..4].SequenceEqual(Magic) &&
        packet[4] == Version &&
        packet[5] == RequestType;

    public static (
        string InstanceId,
        string HostName,
        int Port,
        bool TokenRequired)? DecodeResponse(
        ReadOnlySpan<byte> packet) {
        if (packet.Length < 13 ||
            !packet[..4].SequenceEqual(Magic) ||
            packet[4] != Version ||
            packet[5] != ResponseType) {
            return null;
        }

        var port = BinaryPrimitives.ReadUInt16LittleEndian(
            packet[6..]);
        var tokenRequired = packet[8] != 0;
        var guidLength = BinaryPrimitives.ReadUInt16LittleEndian(
            packet[9..]);
        if (guidLength > packet.Length - 13) {
            return null;
        }

        var hostLengthOffset = 11 + guidLength;
        var hostLength = BinaryPrimitives.ReadUInt16LittleEndian(
            packet[hostLengthOffset..]);
        if (hostLength != packet.Length - hostLengthOffset - 2) {
            return null;
        }

        var guid = Encoding.UTF8.GetString(
            packet.Slice(11, guidLength));
        var host = Encoding.UTF8.GetString(
            packet.Slice(hostLengthOffset + 2, hostLength));
        return (guid, host, port, tokenRequired);
    }
}
