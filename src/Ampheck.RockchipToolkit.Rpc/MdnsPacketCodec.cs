using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

internal static class MdnsPacketCodec {
    private const int HeaderLength = 12;
    private const ushort AuthoritativeResponse = 0x8400;
    private const ushort InternetClass = 1;
    private const ushort CacheFlushClass = 0x8001;

    public static byte[] EncodeQuery(string serviceType) {
        using var stream = new MemoryStream();
        WriteHeader(
            stream,
            flags: 0,
            questions: 1,
            answers: 0,
            authorities: 0,
            additional: 0);
        WriteName(stream, serviceType);
        WriteUInt16(stream, (ushort)MdnsRecordType.Ptr);
        WriteUInt16(stream, InternetClass);
        return stream.ToArray();
    }

    public static byte[] EncodeResponse(
        MdnsResourceRecord pointer,
        IReadOnlyList<MdnsResourceRecord> additional) {
        using var stream = new MemoryStream();
        WriteHeader(
            stream,
            AuthoritativeResponse,
            questions: 0,
            answers: 1,
            authorities: 0,
            additional: checked((ushort)additional.Count));
        WriteRecord(stream, pointer);
        foreach (var record in additional) {
            WriteRecord(stream, record);
        }

        return stream.ToArray();
    }

    public static MdnsPacket Parse(ReadOnlySpan<byte> payload) {
        if (payload.Length < HeaderLength) {
            throw new InvalidDataException(
                "The mDNS packet is too short.");
        }

        var questionCount = BinaryPrimitives.ReadUInt16BigEndian(
            payload[4..]);
        var answerCount = BinaryPrimitives.ReadUInt16BigEndian(
            payload[6..]);
        var authorityCount = BinaryPrimitives.ReadUInt16BigEndian(
            payload[8..]);
        var additionalCount = BinaryPrimitives.ReadUInt16BigEndian(
            payload[10..]);
        var offset = HeaderLength;
        var packet = new MdnsPacket();
        for (var index = 0; index < questionCount; index++) {
            var (name, next) = ReadName(payload, offset);
            offset = next;
            RequireRemaining(payload, offset, 4);
            var type = (MdnsRecordType)
                BinaryPrimitives.ReadUInt16BigEndian(
                    payload[offset..]);
            var recordClass =
                BinaryPrimitives.ReadUInt16BigEndian(
                    payload[(offset + 2)..]);
            offset += 4;
            packet.Questions.Add(new MdnsQuestion(
                name,
                type,
                recordClass));
        }

        for (var index = 0; index < answerCount; index++) {
            offset = ReadRecord(
                payload,
                offset,
                packet.Answers);
        }

        for (var index = 0;
            index < authorityCount + additionalCount;
            index++) {
            offset = ReadRecord(
                payload,
                offset,
                packet.AdditionalRecords);
        }

        return packet;
    }

    public static MdnsResourceRecord CreatePtr(
        string serviceType,
        string instanceName,
        uint ttl) {
        return new MdnsResourceRecord(
            serviceType,
            MdnsRecordType.Ptr,
            InternetClass,
            ttl,
            TargetName: instanceName);
    }

    public static MdnsResourceRecord CreateSrv(
        string instanceName,
        string hostName,
        ushort port,
        uint ttl) {
        return new MdnsResourceRecord(
            instanceName,
            MdnsRecordType.Srv,
            CacheFlushClass,
            ttl,
            TargetName: hostName,
            Port: port);
    }

    public static MdnsResourceRecord CreateTxt(
        string instanceName,
        IReadOnlyList<byte[]> values,
        uint ttl) {
        return new MdnsResourceRecord(
            instanceName,
            MdnsRecordType.Txt,
            CacheFlushClass,
            ttl,
            Text: values);
    }

    public static MdnsResourceRecord CreateA(
        string hostName,
        IPAddress address,
        uint ttl) {
        return new MdnsResourceRecord(
            hostName,
            MdnsRecordType.A,
            CacheFlushClass,
            ttl,
            Address: address);
    }

    public static byte[] ToTxtBytes(string value) {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > byte.MaxValue) {
            throw new ArgumentException(
                "The mDNS TXT value is too long.",
                nameof(value));
        }

        return bytes;
    }

    private static int ReadRecord(
        ReadOnlySpan<byte> payload,
        int offset,
        List<MdnsResourceRecord> destination) {
        var (name, next) = ReadName(payload, offset);
        offset = next;
        RequireRemaining(payload, offset, 10);
        var type = (MdnsRecordType)
            BinaryPrimitives.ReadUInt16BigEndian(
                payload[offset..]);
        var recordClass =
            BinaryPrimitives.ReadUInt16BigEndian(
                payload[(offset + 2)..]);
        var ttl = BinaryPrimitives.ReadUInt32BigEndian(
            payload[(offset + 4)..]);
        var dataLength = BinaryPrimitives.ReadUInt16BigEndian(
            payload[(offset + 8)..]);
        offset += 10;
        RequireRemaining(payload, offset, dataLength);
        var data = payload.Slice(offset, dataLength);
        switch (type) {
            case MdnsRecordType.Ptr:
                var (targetName, _) = ReadName(payload, offset);
                destination.Add(new MdnsResourceRecord(
                    name,
                    type,
                    (ushort)(recordClass & 0x7fff),
                    ttl,
                    TargetName: targetName));
                break;
            case MdnsRecordType.Srv when dataLength >= 7:
                var port = BinaryPrimitives.ReadUInt16BigEndian(
                    data[4..]);
                var (srvTarget, _) = ReadName(payload, offset + 6);
                destination.Add(new MdnsResourceRecord(
                    name,
                    type,
                    (ushort)(recordClass & 0x7fff),
                    ttl,
                    TargetName: srvTarget,
                    Port: port));
                break;
            case MdnsRecordType.Txt:
                var values = new List<byte[]>();
                var textOffset = 0;
                while (textOffset < data.Length) {
                    var length = data[textOffset++];
                    if (textOffset + length > data.Length) {
                        break;
                    }

                    values.Add(data
                        .Slice(textOffset, length)
                        .ToArray());
                    textOffset += length;
                }

                destination.Add(new MdnsResourceRecord(
                    name,
                    type,
                    (ushort)(recordClass & 0x7fff),
                    ttl,
                    Text: values));
                break;
            case MdnsRecordType.A when dataLength == 4:
                destination.Add(new MdnsResourceRecord(
                    name,
                    type,
                    (ushort)(recordClass & 0x7fff),
                    ttl,
                    Address: new IPAddress(data)));
                break;
        }

        return offset + dataLength;
    }

    private static (string Name, int NextOffset) ReadName(
        ReadOnlySpan<byte> payload,
        int offset) {
        var labels = new List<string>();
        var nextOffset = offset;
        var jumped = false;
        var hops = 0;
        while (true) {
            RequireRemaining(payload, offset, 1);
            var length = payload[offset++];
            if (length == 0) {
                if (!jumped) {
                    nextOffset = offset;
                }

                break;
            }

            if ((length & 0xc0) == 0xc0) {
                RequireRemaining(payload, offset, 1);
                var pointer = ((length & 0x3f) << 8) |
                    payload[offset++];
                if (!jumped) {
                    nextOffset = offset;
                    jumped = true;
                }

                offset = pointer;
                if (++hops > 64) {
                    throw new InvalidDataException(
                        "The mDNS name contains a pointer loop.");
                }

                continue;
            }

            if ((length & 0xc0) != 0) {
                throw new InvalidDataException(
                    "The mDNS name uses an unsupported label type.");
            }

            RequireRemaining(payload, offset, length);
            labels.Add(Encoding.UTF8.GetString(
                payload.Slice(offset, length)));
            offset += length;
            if (!jumped) {
                nextOffset = offset;
            }
        }

        return (string.Join('.', labels) + ".", nextOffset);
    }

    private static void WriteRecord(
        Stream stream,
        MdnsResourceRecord record) {
        WriteName(stream, record.Name);
        WriteUInt16(stream, (ushort)record.Type);
        WriteUInt16(stream, record.Class);
        WriteUInt32(stream, record.Ttl);
        using var data = new MemoryStream();
        switch (record.Type) {
            case MdnsRecordType.Ptr:
                WriteName(data, record.TargetName!);
                break;
            case MdnsRecordType.Srv:
                WriteUInt16(data, 0);
                WriteUInt16(data, 0);
                WriteUInt16(data, record.Port);
                WriteName(data, record.TargetName!);
                break;
            case MdnsRecordType.Txt:
                foreach (var value in record.Text ?? []) {
                    if (value.Length > byte.MaxValue) {
                        throw new InvalidDataException(
                            "The mDNS TXT value is too long.");
                    }

                    data.WriteByte((byte)value.Length);
                    data.Write(value);
                }

                break;
            case MdnsRecordType.A:
                var address = record.Address
                    ?? throw new InvalidDataException(
                        "The mDNS A record has no address.");
                data.Write(address.GetAddressBytes());
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported mDNS record type {record.Type}.");
        }

        var payload = data.ToArray();
        WriteUInt16(stream, checked((ushort)payload.Length));
        stream.Write(payload);
    }

    private static void WriteHeader(
        Stream stream,
        ushort flags,
        ushort questions,
        ushort answers,
        ushort authorities,
        ushort additional) {
        WriteUInt16(stream, 0);
        WriteUInt16(stream, flags);
        WriteUInt16(stream, questions);
        WriteUInt16(stream, answers);
        WriteUInt16(stream, authorities);
        WriteUInt16(stream, additional);
    }

    private static void WriteName(Stream stream, string name) {
        foreach (var label in name
            .TrimEnd('.')
            .Split('.')) {
            var bytes = Encoding.UTF8.GetBytes(label);
            if (bytes.Length is 0 or > 63) {
                throw new ArgumentException(
                    $"Invalid mDNS label '{label}'.",
                    nameof(name));
            }

            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }

        stream.WriteByte(0);
    }

    private static void WriteUInt16(Stream stream, ushort value) {
        Span<byte> data = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, value);
        stream.Write(data);
    }

    private static void WriteUInt32(Stream stream, uint value) {
        Span<byte> data = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, value);
        stream.Write(data);
    }

    private static void RequireRemaining(
        ReadOnlySpan<byte> payload,
        int offset,
        int length) {
        if (offset < 0 ||
            length < 0 ||
            offset > payload.Length - length) {
            throw new InvalidDataException(
                "The mDNS packet ended unexpectedly.");
        }
    }
}
