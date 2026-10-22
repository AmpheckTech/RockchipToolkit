using System.Buffers.Binary;
using System.Text;

namespace Ampheck.RockchipToolkit.Rpc;

internal static class ToolkitRequestCodec {
    public static byte[] Encode(ToolkitRequest request) {
        var writer = new ToolkitTlvWriter();
        writer.WriteString(
            ToolkitWireTag.Method,
            ToolkitOperationNames.GetName(request.Operation));
        writer.WriteByte(ToolkitWireTag.Transport, (byte)request.Transport);
        if (request.VendorId is { } vid) {
            writer.WriteUInt16(ToolkitWireTag.VendorId, vid);
        }

        if (request.ProductId is { } pid) {
            writer.WriteUInt16(ToolkitWireTag.ProductId, pid);
        }

        WriteOptionalString(writer, ToolkitWireTag.Loader, request.Loader);
        writer.WriteByte(ToolkitWireTag.LoaderLocation, (byte)request.LoaderLocation);
        writer.WriteByte(ToolkitWireTag.SourceLocation, (byte)request.SourceLocation);
        writer.WriteByte(ToolkitWireTag.FileLocation, (byte)request.FileLocation);
        WriteOptionalString(writer, ToolkitWireTag.Source, request.Source);
        WriteOptionalString(writer, ToolkitWireTag.File, request.File);
        WriteOptionalString(writer, ToolkitWireTag.Backup, request.Backup);
        WriteOptionalString(writer, ToolkitWireTag.SerialPort, request.SerialPort);
        if (request.BaudRate is { } baud) {
            writer.WriteUInt32(
                ToolkitWireTag.SerialBaudRate,
                checked((uint)baud));
        }

        if (request.SerialDataBits is { } bits) {
            writer.WriteUInt32(
                ToolkitWireTag.SerialDataBits,
                checked((uint)bits));
        }

        writer.WriteByte(ToolkitWireTag.SerialParity, (byte)request.SerialParity);
        writer.WriteByte(ToolkitWireTag.SerialStopBits, (byte)request.SerialStopBits);
        writer.WriteByte(ToolkitWireTag.SerialHandshake, (byte)request.SerialHandshake);
        writer.WriteBoolean(ToolkitWireTag.SerialDtrEnable, request.SerialDtrEnable);
        writer.WriteBoolean(ToolkitWireTag.SerialRtsEnable, request.SerialRtsEnable);
        if (request.SerialHistoryBytes is { } history) {
            writer.WriteUInt32(
                ToolkitWireTag.SerialHistoryBytes,
                checked((uint)history));
        }

        if (request.Sector is { } sector) {
            writer.WriteUInt32(ToolkitWireTag.Sector, sector);
        }

        if (request.WriteLength is { } length) {
            writer.WriteUInt64(
                ToolkitWireTag.WriteLength,
                checked((ulong)length));
        }

        if (request.WriteSourceOffset is { } sourceOffset) {
            writer.WriteUInt64(
                ToolkitWireTag.WriteSourceOffset,
                checked((ulong)sourceOffset));
        }

        if (request.Count is { } count) {
            writer.WriteUInt16(ToolkitWireTag.Count, count);
        }

        if (request.ResetMode is { } reset) {
            writer.WriteByte(ToolkitWireTag.ResetMode, (byte)reset);
        }

        if (request.Storage is { } storage) {
            writer.WriteByte(ToolkitWireTag.Storage, (byte)storage);
        }

        writer.WriteBoolean(ToolkitWireTag.EraseBeforeWrite, request.EraseBeforeWrite);
        writer.WriteBoolean(ToolkitWireTag.ResetAfterFlash, request.ResetAfterFlash);
        writer.WriteBoolean(ToolkitWireTag.Verbose, request.Verbose);
        foreach (var attachment in request.Attachments) {
            writer.Write(ToolkitWireTag.Attachment, EncodeAttachment(attachment));
        }

        return writer.ToArray();
    }

    public static ToolkitRequest Decode(ReadOnlySpan<byte> payload) {
        var reader = new ToolkitTlvReader(payload);
        var method = reader.GetString(ToolkitWireTag.Method)
            ?? throw new InvalidDataException("The toolkit method is missing.");
        if (!ToolkitOperationNames.TryParse(method, out var operation)) {
            throw new UnsupportedToolkitMethodException(method);
        }

        return new ToolkitRequest {
            Operation = operation,
            Transport = ReadEnum(reader, ToolkitWireTag.Transport, ToolkitTransport.Auto),
            VendorId = reader.GetUInt16(ToolkitWireTag.VendorId),
            ProductId = reader.GetUInt16(ToolkitWireTag.ProductId),
            Loader = reader.GetString(ToolkitWireTag.Loader),
            LoaderLocation = ReadEnum(reader, ToolkitWireTag.LoaderLocation, ToolkitInputKind.LocalPath),
            SourceLocation = ReadEnum(reader, ToolkitWireTag.SourceLocation, ToolkitInputKind.LocalPath),
            FileLocation = ReadEnum(reader, ToolkitWireTag.FileLocation, ToolkitInputKind.LocalPath),
            Source = reader.GetString(ToolkitWireTag.Source),
            File = reader.GetString(ToolkitWireTag.File),
            Backup = reader.GetString(ToolkitWireTag.Backup),
            SerialPort = reader.GetString(ToolkitWireTag.SerialPort),
            BaudRate = ToInt32(reader.GetUInt32(ToolkitWireTag.SerialBaudRate)),
            SerialDataBits = ToInt32(reader.GetUInt32(ToolkitWireTag.SerialDataBits)),
            SerialParity = ReadEnum(reader, ToolkitWireTag.SerialParity, ToolkitSerialParity.None),
            SerialStopBits = ReadEnum(reader, ToolkitWireTag.SerialStopBits, ToolkitSerialStopBits.One),
            SerialHandshake = ReadEnum(reader, ToolkitWireTag.SerialHandshake, ToolkitSerialHandshake.None),
            SerialDtrEnable = reader.GetBoolean(ToolkitWireTag.SerialDtrEnable) ?? true,
            SerialRtsEnable = reader.GetBoolean(ToolkitWireTag.SerialRtsEnable) ?? true,
            SerialHistoryBytes = ToInt32(reader.GetUInt32(ToolkitWireTag.SerialHistoryBytes)),
            Sector = reader.GetUInt32(ToolkitWireTag.Sector),
            WriteLength = ToInt64(reader.GetUInt64(ToolkitWireTag.WriteLength)),
            WriteSourceOffset = ToInt64(reader.GetUInt64(ToolkitWireTag.WriteSourceOffset)),
            Count = reader.GetUInt16(ToolkitWireTag.Count),
            ResetMode = ReadNullableEnum<ToolkitResetMode>(reader, ToolkitWireTag.ResetMode),
            Storage = ReadNullableEnum<ToolkitStorage>(reader, ToolkitWireTag.Storage),
            EraseBeforeWrite = reader.GetBoolean(ToolkitWireTag.EraseBeforeWrite) ?? true,
            ResetAfterFlash = reader.GetBoolean(ToolkitWireTag.ResetAfterFlash) ?? true,
            Verbose = reader.GetBoolean(ToolkitWireTag.Verbose) ?? false,
            Attachments = reader.GetValues(ToolkitWireTag.Attachment)
                .Select(DecodeAttachment)
                .ToArray()
        };
    }

    private static byte[] EncodeAttachment(ToolkitAttachment attachment) {
        var name = Encoding.UTF8.GetBytes(attachment.FileName);
        if (name.Length > ushort.MaxValue) {
            throw new InvalidDataException("The attachment file name is too long.");
        }

        var result = new byte[8 + name.Length];
        result[0] = (byte)attachment.Role;
        result[1] = (byte)attachment.Kind;
        BinaryPrimitives.WriteUInt32LittleEndian(
            result.AsSpan(2),
            checked((uint)attachment.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(
            result.AsSpan(6),
            (ushort)name.Length);
        name.CopyTo(result, 8);
        return result;
    }

    private static ToolkitAttachment DecodeAttachment(byte[] data) {
        if (data.Length < 8) {
            throw new InvalidDataException("The attachment descriptor is invalid.");
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6));
        if (data.Length != 8 + nameLength) {
            throw new InvalidDataException("The attachment name length is invalid.");
        }

        return new ToolkitAttachment(
            (ToolkitAttachmentRole)data[0],
            (ToolkitAttachmentKind)data[1],
            Encoding.UTF8.GetString(data, 8, nameLength),
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(2)));
    }

    private static void WriteOptionalString(ToolkitTlvWriter writer, ToolkitWireTag tag, string? value) {
        if (!string.IsNullOrWhiteSpace(value)) {
            writer.WriteString(tag, value);
        }
    }

    private static T ReadEnum<T>(ToolkitTlvReader reader, ToolkitWireTag tag, T fallback) where T : struct, Enum {
        if (reader.GetByte(tag) is not { } value) {
            return fallback;
        }

        var result = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(result)
            ? result
            : throw new InvalidDataException(
                $"Invalid value {value} for toolkit field {tag}.");
    }

    private static long? ToInt64(ulong? value) =>
        value switch {
            null => null,
            > long.MaxValue => throw new InvalidDataException(
                "A toolkit integer value is out of range."),
            _ => (long)value.Value
        };

    private static int? ToInt32(uint? value) =>
        value switch {
            null => null,
            > int.MaxValue => throw new InvalidDataException(
                "A toolkit integer value is out of range."),
            _ => (int)value.Value
        };

    private static T? ReadNullableEnum<T>(ToolkitTlvReader reader, ToolkitWireTag tag) where T : struct, Enum {
        if (reader.GetByte(tag) is not { } value) {
            return null;
        }

        var result = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(result)
            ? result
            : throw new InvalidDataException(
                $"Invalid value {value} for toolkit field {tag}.");
    }
}
