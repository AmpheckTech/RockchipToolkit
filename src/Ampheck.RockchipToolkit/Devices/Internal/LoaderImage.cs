using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Images.Rkfw;

namespace Ampheck.RockchipToolkit.Devices.Internal;

internal sealed class LoaderImage(RockchipImage image) : IDisposable {
    public bool HasNewIdbHeader => image.BootKind.IsNewIdb();
    public int HeaderSize => image.BootHeaderSize;
    public IReadOnlyList<LoaderImageEntry> Entries { get; } =
        [.. image.BootEntries
            .Select(entry => new LoaderImageEntry(
                entry.Name,
                entry.Type switch {
                    BootEntryType.Code471 => 0x0471,
                    BootEntryType.Code472 => 0x0472,
                    _ => 0
                },
                entry.Offset,
                entry.Size,
                entry.Delay))];

    public Stream OpenBootRange(uint offset, uint size) =>
        image.OpenBootRange(offset, size);

    public void Dispose() => image.Dispose();
}
