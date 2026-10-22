namespace Ampheck.RockchipToolkit.Images;

/// <summary>Reads packaged partition images from a Rockchip RKFW update.img.</summary>
public sealed class RockchipUpdateImage : IDisposable {
    private readonly FileStream _stream;
    private readonly object _streamLock = new();
    private bool _disposed;

    private RockchipUpdateImage(
        FileStream stream,
        RockchipImageHeader containerHeader,
        uint version,
        string machineModel,
        string manufacturer,
        IReadOnlyList<RockchipUpdateEntry> entries) {
        _stream = stream;
        ContainerHeader = containerHeader;
        Version = version;
        MachineModel = machineModel;
        Manufacturer = manufacturer;
        Entries = entries;
    }

    public RockchipImageHeader ContainerHeader { get; }
    public uint Version { get; }
    public string MachineModel { get; }
    public string Manufacturer { get; }
    public IReadOnlyList<RockchipUpdateEntry> Entries { get; }

    public static RockchipUpdateImage Open(string path) {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        try {
            var update = RockchipUpdateImageReader.Read(stream);
            return new RockchipUpdateImage(
                stream,
                update.ContainerHeader,
                update.Version,
                update.MachineModel,
                update.Manufacturer,
                update.Entries);
        } catch {
            stream.Dispose();
            throw;
        }
    }

    public RockchipUpdateEntry? FindEntry(string name) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Entries.FirstOrDefault(entry =>
            entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public byte[] ReadEntryBytes(RockchipUpdateEntry entry) {
        if (entry.Size > int.MaxValue) {
            throw new InvalidDataException(
                $"Update entry '{entry.Name}' is too large to load.");
        }

        var data = new byte[checked((int)entry.Size)];
        using var stream = OpenEntryStream(entry);
        StreamRange.ReadExactly(stream, data);
        return data;
    }

    public Stream OpenEntryStream(RockchipUpdateEntry entry) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Entries.Contains(entry)) {
            throw new ArgumentException(
                "The entry does not belong to this update image.",
                nameof(entry));
        }

        StreamRange.Validate(
            _stream.Length,
            entry.Offset,
            entry.Size,
            $"update entry '{entry.Name}'");
        return new RockchipImageSubStream(
            _stream,
            _streamLock,
            entry.Offset,
            entry.Size);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }
}
