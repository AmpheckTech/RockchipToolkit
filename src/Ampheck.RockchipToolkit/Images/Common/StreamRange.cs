namespace Ampheck.RockchipToolkit.Images.Common;

internal static class StreamRange {
    public static void Validate(
        long containerLength,
        long offset,
        long length,
        string description
    ) {
        if (offset < 0 ||
            length < 0 ||
            offset > containerLength ||
            length > containerLength - offset) {
            throw new InvalidDataException($"The {description} range is outside the image.");
        }
    }

    public static void ReadExactly(Stream stream, Span<byte> buffer, long? offset = null) {
        if (offset.HasValue) {
            stream.Position = offset.Value;
        }

        while (!buffer.IsEmpty) {
            var read = stream.Read(buffer);
            if (read == 0) {
                throw new EndOfStreamException("The Rockchip image ended unexpectedly.");
            }

            buffer = buffer[read..];
        }
    }

    public static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken
    ) {
        while (!buffer.IsEmpty) {
            var read = await stream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException(
                    "The Rockchip image ended unexpectedly.");
            }

            buffer = buffer[read..];
        }
    }
}
