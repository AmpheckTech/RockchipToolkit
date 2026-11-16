using Ampheck.RockchipToolkit.Features.Devices.Adapters;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;
using Ampheck.RockchipToolkit.Images.Rkfw;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Devices.Services;

public sealed class RockchipBootLoader {
    // MaskROM bootstrap transfer IDs, as used by Barebox rk-usb-loader.
    private const ushort Code471 = 0x0471;
    private const ushort Code472 = 0x0472;

    public async ValueTask DownloadAsync(
        RockUsbProtocol protocol,
        UsbDeviceInfo device,
        bool hasNativeMaskRomPath,
        string path,
        CancellationToken cancellationToken,
        ILogger logger
    ) {
        if (!hasNativeMaskRomPath &&
            (device.Mode & RockchipUsbMode.MaskRom) == 0) {
            throw new InvalidOperationException(
                "BOOT download requires a device in MaskROM mode.");
        }

        using var image = new LoaderImage(RockchipImage.Open(path));
        logger.LogDebug(
            "Opened loader image {LoaderPath}",
            path);
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
        LoaderImage image
    ) {
        var transfers = new List<BootTransfer>();
        if (image.HasNewIdbHeader) {
            transfers.Add(new BootTransfer(
                "new IDB header",
                Code471,
                0,
                checked((uint)image.HeaderSize),
                0));
        }

        foreach (var entry in image.Entries) {
            if (entry.RequestCode is not (Code471 or Code472)) {
                continue;
            }

            transfers.Add(new BootTransfer(
                $"BOOT entry {entry.Name}",
                entry.RequestCode,
                entry.Offset,
                entry.Size,
                entry.Delay));
        }

        return transfers.AsReadOnly();
    }

    private static async ValueTask SendTransferAsync(
        RockUsbProtocol protocol,
        LoaderImage image,
        BootTransfer transfer,
        CancellationToken cancellationToken,
        ILogger logger
    ) {
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
        CancellationToken cancellationToken
    ) {
        if (size > int.MaxValue) {
            throw new InvalidDataException($"{description} is too large.");
        }

        var data = new byte[checked((int)size)];
        await stream.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        return data;
    }

}
