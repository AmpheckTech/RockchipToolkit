namespace Ampheck.RockchipToolkit.Images;

/// <summary>Reads RKIMAGE and BOOT headers and exposes their payloads without loading the whole file.</summary>
public sealed class RockchipImage : IDisposable {
    private readonly FileStream _stream;
    private readonly object _streamLock = new();
    private bool _disposed;

    public RockchipImageHeader Header { get; }
    public IReadOnlyList<RockchipBootEntry> BootEntries { get; }

    private RockchipImage(
        FileStream stream,
        RockchipImageHeader header,
        IReadOnlyList<RockchipBootEntry> bootEntries) {
        _stream = stream;
        Header = header;
        BootEntries = bootEntries;
    }

    /// <summary>Opens an RKIMAGE container or a standalone Rockchip BOOT image.</summary>
    public static RockchipImage Open(string path) {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var stream = File.OpenRead(path);
        try {
            var result = RockchipImageReader.Read(stream);
            return new RockchipImage(stream, result.Header, result.Entries);
        } catch {
            stream.Dispose();
            throw;
        }
    }

    public Stream OpenBootStream() {
        return OpenRange(Header.BootOffset, Header.BootSize);
    }

    public Stream OpenFirmwareStream() {
        return OpenRange(Header.FirmwareOffset, Header.FirmwareSize);
    }

    public Stream OpenBootEntryStream(RockchipBootEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        if (!BootEntries.Contains(entry)) {
            throw new ArgumentException("The entry does not belong to this image.", nameof(entry));
        }

        return OpenRange(checked((long)Header.BootOffset + entry.Offset), entry.Size);
    }

    private RockchipImageSubStream OpenRange(long offset, long length) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StreamRange.Validate(_stream.Length, offset, length, "image");
        return new RockchipImageSubStream(_stream, _streamLock, offset, length);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }
}
