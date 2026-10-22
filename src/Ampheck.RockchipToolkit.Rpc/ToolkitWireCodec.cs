using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

internal static class ToolkitWireCodec {
    public const int HeaderLength = 36;
    public const int MaximumPayloadLength = 1024 * 1024;
    private static ReadOnlySpan<byte> Magic => "ARKT"u8;

    public static byte[] Encode(ToolkitFrame frame) {
        if (frame.Payload.Length > MaximumPayloadLength) {
            throw new InvalidDataException("The toolkit frame is too large.");
        }

        var result = new byte[HeaderLength + frame.Payload.Length];
        Magic.CopyTo(result);
        result[4] = frame.ProtocolMajor;
        result[5] = frame.ProtocolMinor;
        result[6] = (byte)frame.Kind;
        result[7] = (byte)frame.Flags;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), (ushort)frame.Opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), frame.ChannelId);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), frame.RequestId);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(20), frame.Sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(28), (uint)frame.Payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(32), frame.Status);
        frame.Payload.CopyTo(result, HeaderLength);
        return result;
    }

    public static ToolkitFrame Decode(ReadOnlySpan<byte> data) {
        if (data.Length < HeaderLength || !data[..4].SequenceEqual(Magic)) {
            throw new InvalidDataException("The toolkit frame header is invalid.");
        }

        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]);
        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(data[28..]);
        if (headerLength < HeaderLength || headerLength > data.Length ||
            payloadLength > MaximumPayloadLength ||
            headerLength > data.Length - (int)payloadLength) {
            throw new InvalidDataException("The toolkit frame length is invalid.");
        }

        return new ToolkitFrame(
            data[4],
            data[5],
            (ToolkitFrameKind)data[6],
            data[7],
            (ToolkitOpcode)BinaryPrimitives.ReadUInt16LittleEndian(data[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(data[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[20..]),
            BinaryPrimitives.ReadUInt16LittleEndian(data[32..]),
            data.Slice(headerLength, (int)payloadLength).ToArray());
    }

    public static ToolkitFrame HelloRequest(
        uint requestId,
        string clientVersion,
        string? token,
        string? clientName = null) {
        var writer = new ToolkitTlvWriter();
        writer.WriteString(ToolkitWireTag.ClientVersion, clientVersion);
        if (!string.IsNullOrWhiteSpace(clientName)) {
            writer.WriteString(ToolkitWireTag.ClientName, clientName);
        }

        if (!string.IsNullOrEmpty(token)) {
            writer.WriteString(ToolkitWireTag.Token, token);
        }

        return CreateFrame(ToolkitFrameKind.Request, ToolkitOpcode.Hello, 0, requestId, writer.ToArray());
    }

    public static ToolkitFrame HelloResponse(
        uint requestId,
        ToolkitStatus status,
        ToolkitHello? hello) {
        if (hello is null) {
            return CreateFrame(ToolkitFrameKind.Response, ToolkitOpcode.Hello, 0, requestId, [], status);
        }

        var writer = new ToolkitTlvWriter();
        writer.WriteString(ToolkitWireTag.ServerGuid, hello.ServerGuid);
        writer.WriteString(ToolkitWireTag.ConnectionGuid, hello.ConnectionGuid);
        writer.WriteString(ToolkitWireTag.HostName, hello.HostName);
        writer.WriteString(ToolkitWireTag.ServerVersion, hello.ServerVersion);
        foreach (var method in hello.Methods) {
            writer.WriteString(ToolkitWireTag.Method, method);
        }

        foreach (var capability in hello.Capabilities) {
            writer.WriteString(ToolkitWireTag.Capability, capability);
        }

        return CreateFrame(ToolkitFrameKind.Response, ToolkitOpcode.Hello, 0, requestId, writer.ToArray(), status);
    }

    public static ToolkitFrame Response(
        uint channelId,
        uint requestId,
        ToolkitStatus status,
        ReadOnlySpan<byte> payload = default) {
        return CreateFrame(ToolkitFrameKind.Response, ToolkitOpcode.Call, channelId, requestId, payload, status);
    }

    public static ToolkitFrame ErrorResponse(
        uint channelId,
        uint requestId,
        ToolkitStatus status,
        string message) {
        var writer = new ToolkitTlvWriter();
        writer.WriteString(ToolkitWireTag.ErrorMessage, message);
        return Response(channelId, requestId, status, writer.ToArray());
    }

    public static ToolkitFrame Notify(
        uint channelId,
        uint requestId,
        ulong sequence,
        string source,
        string message) {
        var writer = new ToolkitTlvWriter();
        writer.WriteString(ToolkitWireTag.LogSource, source);
        writer.WriteString(ToolkitWireTag.LogMessage, message);
        return CreateFrame(
            ToolkitFrameKind.Notify,
            ToolkitOpcode.Log,
            channelId,
            requestId,
            writer.ToArray(),
            sequence: sequence);
    }

    public static ToolkitFrame Progress(
        uint channelId,
        uint requestId,
        ulong sequence,
        string label,
        long current,
        long total) {
        var writer = new ToolkitTlvWriter();
        writer.WriteString(ToolkitWireTag.ProgressLabel, label);
        writer.WriteUInt64(
            ToolkitWireTag.ProgressCurrent,
            checked((ulong)current));
        writer.WriteUInt64(
            ToolkitWireTag.ProgressTotal,
            checked((ulong)total));
        return CreateFrame(
            ToolkitFrameKind.Notify,
            ToolkitOpcode.Progress,
            channelId,
            requestId,
            writer.ToArray(),
            sequence: sequence);
    }

    public static ToolkitFrame Data(uint channelId, uint requestId, ReadOnlyMemory<byte> payload) {
        return CreateFrame(ToolkitFrameKind.Data, ToolkitOpcode.Call, channelId, requestId, payload.Span);
    }

    public static ToolkitFrame AttachmentEnd(uint channelId, uint requestId) {
        return CreateFrame(ToolkitFrameKind.AttachmentEnd, ToolkitOpcode.Call, channelId, requestId, []);
    }

    public static ToolkitFrame Cancel(uint channelId, uint requestId) {
        return CreateFrame(ToolkitFrameKind.Cancel, ToolkitOpcode.Cancel, channelId, requestId, []);
    }

    public static ToolkitFrame Close(uint channelId, uint requestId) {
        return CreateFrame(ToolkitFrameKind.Close, ToolkitOpcode.CloseChannel, channelId, requestId, []);
    }

    public static ToolkitFrame CreateFrame(
        ToolkitFrameKind kind,
        ToolkitOpcode opcode,
        uint channelId,
        uint requestId,
        ReadOnlySpan<byte> payload,
        ToolkitStatus status = ToolkitStatus.Ok,
        ulong sequence = 0) {
        return new ToolkitFrame(
            ToolkitProtocolVersion.Major,
            ToolkitProtocolVersion.Minor,
            kind,
            Flags: 0,
            opcode,
            channelId,
            requestId,
            sequence,
            (ushort)status,
            payload.ToArray());
    }

    public static string GetLogSource(ToolkitFrame frame) =>
        new ToolkitTlvReader(frame.Payload).GetString(ToolkitWireTag.LogSource) ?? "stdout";

    public static string GetLogMessage(ToolkitFrame frame) =>
        new ToolkitTlvReader(frame.Payload).GetString(ToolkitWireTag.LogMessage) ?? string.Empty;

    public static ToolkitEvent GetProgress(ToolkitFrame frame) {
        var reader = new ToolkitTlvReader(frame.Payload);
        return new ToolkitEvent(
            "progress",
            reader.GetString(ToolkitWireTag.ProgressLabel) ?? "Progress",
            ToInt64(reader.GetUInt64(
                ToolkitWireTag.ProgressCurrent)),
            ToInt64(reader.GetUInt64(
                ToolkitWireTag.ProgressTotal)));
    }

    public static string? GetErrorMessage(ToolkitFrame frame) =>
        new ToolkitTlvReader(frame.Payload).GetString(ToolkitWireTag.ErrorMessage);

    public static ToolkitHello? DecodeHello(ToolkitFrame frame) {
        var reader = new ToolkitTlvReader(frame.Payload);
        var guid = reader.GetString(ToolkitWireTag.ServerGuid);
        if (guid is null) {
            return null;
        }

        return new ToolkitHello(
            guid,
            reader.GetString(ToolkitWireTag.ConnectionGuid) ?? string.Empty,
            reader.GetString(ToolkitWireTag.HostName) ?? "unknown",
            reader.GetString(ToolkitWireTag.ServerVersion) ?? "unknown",
            frame.ProtocolMajor,
            frame.ProtocolMinor,
            reader.GetValues(ToolkitWireTag.Method).Select(Encoding.UTF8.GetString).ToArray(),
            reader.GetValues(ToolkitWireTag.Capability).Select(Encoding.UTF8.GetString).ToArray());
    }

    public static bool IsValidFrame(ToolkitFrame frame) =>
        frame.ProtocolMajor == ToolkitProtocolVersion.Major;

    private static long? ToInt64(ulong? value) =>
        value is null ? null : checked((long)value.Value);
}
