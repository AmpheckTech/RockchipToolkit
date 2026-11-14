using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Device;

internal static class RockchipBootLoader {
    // MaskROM bootstrap transfer IDs, as used by Barebox rk-usb-loader.
    private const ushort Code471 = 0x0471;
    private const ushort Code472 = 0x0472;

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
        var transfers = GetTransfers(image);
        if (transfers.Count == 0) {
            throw new InvalidDataException(
                "The loader contains no 471 or 472 boot transfers.");
        }

        logger.LogInformation(
            "Downloading {BootTransferCount} boot transfer(s)",
            transfers.Count);
        foreach (var transfer in transfers) {
            await SendTransferAsync(
                protocol,
                image,
                transfer,
                cancellationToken,
                logger).ConfigureAwait(false);
            if (transfer.Delay > 0) {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(transfer.Delay),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await Task.Delay(
            TimeSpan.FromSeconds(1),
            cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Loader download completed");
    }

    private static IReadOnlyList<BootTransfer> GetTransfers(
        RockchipImage image) {
        var transfers = new List<BootTransfer>();
        if (image.BootKind.IsNewIdb()) {
            transfers.Add(new BootTransfer(
                $"{image.BootKind} header",
                Code471,
                0,
                image.BootHeaderSize,
                0));
        }

        foreach (var entry in image.BootEntries) {
            if (entry.Type is not (BootEntryType.Code471 or BootEntryType.Code472)) {
                continue;
            }

            transfers.Add(new BootTransfer(
                $"BOOT entry {entry.Name}",
                entry.Type == BootEntryType.Code471 ? Code471 : Code472,
                entry.Offset,
                entry.Size,
                entry.Delay));
        }

        return transfers.AsReadOnly();
    }

    private static async ValueTask SendTransferAsync(
        RockUsbProtocol protocol,
        RockchipImage image,
        BootTransfer transfer,
        CancellationToken cancellationToken,
        ILogger logger) {
        byte[] data;
        await using (var stream = image.OpenBootRange(
            transfer.Offset,
            transfer.Size)) {
            data = await ReadAsync(
                    stream,
                    transfer.Size,
                    transfer.Description,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        logger.LogDebug(
            "Sending {Description} as 0x{RequestCode:X4} " +
            "({ByteCount} bytes)",
            transfer.Description,
            transfer.RequestCode,
            data.Length);
        await protocol.DownloadBootEntryAsync(
            transfer.RequestCode,
            data,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> ReadAsync(
        Stream stream,
        long size,
        string description,
        CancellationToken cancellationToken) {
        if (size > int.MaxValue) {
            throw new InvalidDataException($"{description} is too large.");
        }

        var data = new byte[checked((int)size)];
        await stream.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        return data;
    }

    private sealed record BootTransfer(
        string Description,
        ushort RequestCode,
        uint Offset,
        uint Size,
        uint Delay);
}
