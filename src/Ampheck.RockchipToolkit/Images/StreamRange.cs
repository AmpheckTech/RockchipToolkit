namespace Ampheck.RockchipToolkit.Images;

internal static class StreamRange {
    public static void Validate(
        long containerLength,
        long offset,
        long length,
        string description) {
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
}
