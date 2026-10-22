using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Device;

internal static class RockchipBootLoader {
    public static async ValueTask LoadAsync(
        RockUsbProtocol protocol,
        UsbDeviceInfo device,
        bool hasNativeMaskRomPath,
        string path,
        CancellationToken cancellationToken,
        ILogger logger) {
        if (!hasNativeMaskRomPath &&
            (device.Mode & RockchipUsbMode.MaskRom) == 0) {
            throw new InvalidOperationException(
                "BOOT download requires a device in MaskROM mode.");
        }

        using var image = RockchipImage.Open(path);
        logger.LogDebug(
            "Opened loader image {LoaderPath} ({ImageKind})",
            path,
            image.Header.Kind);
        var entries = image.BootEntries
            .Where(entry =>
                entry.Type is
                    BootEntryType.Code471 or
                    BootEntryType.Code472)
            .ToArray();
        if (entries.Length == 0) {
            throw new InvalidDataException(
                "The loader contains no 471 or 472 BOOT entries.");
        }

        logger.LogInformation(
            "Downloading {BootEntryCount} BOOT entry/entries",
            entries.Length);
        foreach (var entry in entries) {
            await SendEntryAsync(
                protocol,
                image,
                entry,
                cancellationToken,
                logger).ConfigureAwait(false);
            if (entry.Delay > 0) {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(entry.Delay),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await Task.Delay(
            TimeSpan.FromSeconds(1),
            cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Loader download completed");
    }

    private static async ValueTask SendEntryAsync(
        RockUsbProtocol protocol,
        RockchipImage image,
        RockchipBootEntry entry,
        CancellationToken cancellationToken,
        ILogger logger) {
        if (entry.Size > int.MaxValue) {
            throw new InvalidDataException(
                $"BOOT entry '{entry.Name}' is too large.");
        }

        var data = new byte[checked((int)entry.Size)];
        await using (var stream = image.OpenBootEntryStream(entry)) {
            await stream.ReadExactlyAsync(data, cancellationToken)
                .ConfigureAwait(false);
        }

        var requestCode = entry.Type == BootEntryType.Code471
            ? (ushort)0x0471
            : (ushort)0x0472;
        logger.LogDebug(
            "Sending BOOT entry {EntryName} as 0x{RequestCode:X4} " +
            "({ByteCount} bytes)",
            entry.Name,
            requestCode,
            data.Length);
        await protocol.DownloadBootEntryAsync(
            requestCode,
            data,
            cancellationToken).ConfigureAwait(false);
    }
}
