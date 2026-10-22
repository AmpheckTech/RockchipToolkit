namespace Ampheck.RockchipToolkit.Transport;

/// <summary>Optional native MaskROM BOOT download path exposed by some transports.</summary>
public interface IMaskRomBootTransport {
    /// <summary>Transfers one raw 0x0471 or 0x0472 BOOT entry and delegates CRC generation to the transport.</summary>
    ValueTask DownloadBootEntryAsync(
        ushort requestCode,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}
