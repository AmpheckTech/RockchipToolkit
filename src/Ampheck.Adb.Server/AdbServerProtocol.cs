using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Ampheck.Adb.Server;

internal static class AdbServerProtocol {
    private const int MaximumRequestLength = 1024 * 1024;

    public static async ValueTask<string?> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        var header = new byte[4];
        if (!await TryReadExactlyAsync(
                stream,
                header,
                cancellationToken)
            .ConfigureAwait(false)) {
            return null;
        }

        var lengthText = Encoding.ASCII.GetString(header);
        if (!int.TryParse(
                lengthText,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var length) ||
            length <= 0 ||
            length > MaximumRequestLength) {
            throw new InvalidDataException(
                "The ADB client sent an invalid request length.");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(
                stream,
                payload,
                cancellationToken)
            .ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    public static ValueTask WriteStatusAsync(
        Stream stream,
        string status,
        CancellationToken cancellationToken) {
        if (status.Length != 4) {
            throw new ArgumentException(
                "ADB status must contain exactly four characters.",
                nameof(status));
        }

        return stream.WriteAsync(
            Encoding.ASCII.GetBytes(status),
            cancellationToken);
    }

    public static async ValueTask WritePayloadAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken) {
        var header = Encoding.ASCII.GetBytes(
            payload.Length.ToString("x4", CultureInfo.InvariantCulture));
        await stream.WriteAsync(header, cancellationToken)
            .ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }

    public static ValueTask WritePayloadAsync(
        Stream stream,
        string payload,
        CancellationToken cancellationToken) {
        return WritePayloadAsync(
            stream,
            Encoding.UTF8.GetBytes(payload),
            cancellationToken);
    }

    public static async ValueTask WriteFailureAsync(
        Stream stream,
        string message,
        CancellationToken cancellationToken) {
        await WriteStatusAsync(
                stream,
                "FAIL",
                cancellationToken)
            .ConfigureAwait(false);
        await WritePayloadAsync(
                stream,
                message,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask WriteTransportIdAsync(
        Stream stream,
        ulong transportId,
        CancellationToken cancellationToken) {
        await WriteStatusAsync(
                stream,
                "OKAY",
                cancellationToken)
            .ConfigureAwait(false);
        var payload = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(
            payload,
            transportId);
        await stream.WriteAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken) {
        if (!await TryReadExactlyAsync(
                stream,
                buffer,
                cancellationToken)
            .ConfigureAwait(false)) {
            throw new EndOfStreamException(
                "The ADB connection ended unexpectedly.");
        }
    }

    private static async ValueTask<bool> TryReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken) {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(
                    buffer[offset..],
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                return offset == 0
                    ? false
                    : throw new EndOfStreamException(
                        "The ADB request ended unexpectedly.");
            }

            offset += read;
        }

        return true;
    }
}
