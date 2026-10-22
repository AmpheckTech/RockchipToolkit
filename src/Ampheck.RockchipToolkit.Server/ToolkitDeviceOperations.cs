using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Protocol;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server.Devices;
using Ampheck.RockchipToolkit.Server.Firmware;
using Ampheck.RockchipToolkit.Server.IO;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

internal static class ToolkitDeviceOperations {
    public static async Task ExecuteAsync(
        ToolkitRequest request,
        FlashInput? flashInput,
        IUsbTransport transport,
        ILoggerFactory loggerFactory,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        await using var device = new RockchipDevice(
            transport,
            loggerFactory: loggerFactory);
        switch (request.Operation) {
            case ToolkitOperation.Info:
                await ReportInfoAsync(
                        device,
                        report,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.ChipInfo:
                await ReportAsync(
                        report,
                        ToolkitInfoFormatter.FormatChip(
                            RockchipChipInfoParser.Parse(
                                await device.GetChipInfoAsync(
                                        cancellationToken)
                                    .ConfigureAwait(false))))
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.ChipId:
                await ReportAsync(
                        report,
                        Convert.ToHexString(
                            await device.GetFlashIdAsync(
                                    cancellationToken)
                                .ConfigureAwait(false)))
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Capability:
                await ReportAsync(
                        report,
                        ToolkitInfoFormatter.FormatCapabilities(
                            RockchipCapabilityParser.Parse(
                                await device.GetCapabilityAsync(
                                        cancellationToken)
                                    .ConfigureAwait(false))))
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Loader:
                await DownloadLoaderAsync(
                        request,
                        device,
                        logger,
                        report,
                        (current, total) => ReportProgress(
                            report,
                            "loader",
                            current,
                            total),
                        limits,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Flash:
                await FlashAsync(
                        request,
                        flashInput,
                        device,
                        logger,
                        report,
                        (current, total) => ReportProgress(
                            report,
                            "firmware",
                            current,
                            total),
                        limits,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Read:
                await ReadAsync(
                        request,
                        device,
                        logger,
                        report,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Write:
                await WriteAsync(
                        request,
                        device,
                        logger,
                        report,
                        (current, total) => ReportProgress(
                            report,
                            "write",
                            current,
                            total),
                        limits,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Erase:
                await ReportStatusAsync(
                        report,
                        $"Erasing {request.Count ?? 1:N0} sectors at " +
                        $"0x{(request.Sector ?? 0):X8}")
                    .ConfigureAwait(false);
                await device.EraseAsync(
                        request.Sector ?? 0,
                        request.Count ?? 1,
                        cancellationToken)
                    .ConfigureAwait(false);
                await ReportSuccessAsync(report, "Erase complete")
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Storage:
                await ReportStatusAsync(
                        report,
                        $"Changing storage to {request.Storage}")
                    .ConfigureAwait(false);
                await device.ChangeStorageAsync(
                        ToStorageType(request.Storage),
                        cancellationToken)
                    .ConfigureAwait(false);
                await ReportSuccessAsync(
                        report,
                        $"Storage changed to {request.Storage}")
                    .ConfigureAwait(false);
                break;
            case ToolkitOperation.Reset:
                await ReportStatusAsync(
                        report,
                        "Requesting device reset")
                    .ConfigureAwait(false);
                await device.ResetAsync(
                        ToResetMode(request.ResetMode),
                        cancellationToken)
                    .ConfigureAwait(false);
                await device.DisposeAsync()
                    .ConfigureAwait(false);
                await ReportSuccessAsync(report, "Reset requested")
                    .ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.Operation,
                    "The toolkit operation is not supported.");
        }
    }

    private static async Task ReportInfoAsync(
        RockchipDevice device,
        Func<ToolkitEvent, ValueTask> report,
        CancellationToken cancellationToken) {
        var chipInfo = RockchipChipInfoParser.Parse(
            await device.GetChipInfoAsync(cancellationToken)
                .ConfigureAwait(false));
        var flashInfo = RockchipFlashInfoParser.Parse(
            await device.GetFlashInfoAsync(cancellationToken)
                .ConfigureAwait(false));
        await ReportAsync(
                report,
                ToolkitInfoFormatter.FormatChip(chipInfo) +
                Environment.NewLine +
                Environment.NewLine +
                ToolkitInfoFormatter.FormatFlash(flashInfo))
            .ConfigureAwait(false);
    }

    private static async Task DownloadLoaderAsync(
        ToolkitRequest request,
        RockchipDevice device,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        Func<long, long, ValueTask> progress,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        var location = request.File ?? request.Loader;
        if (string.IsNullOrWhiteSpace(location)) {
            throw new InvalidDataException(
                "The loader request contains no file.");
        }

        await using var loader = await InputFile.OpenAsync(
                location,
                logger,
                message => ReportStatusAsync(report, message),
                progress,
                cancellationToken,
                limits: limits)
            .ConfigureAwait(false);
        await device.DownloadBootAsync(
                loader.Path,
                cancellationToken)
            .ConfigureAwait(false);
        await ReportSuccessAsync(report, "Bootloader downloaded")
            .ConfigureAwait(false);
    }

    private static async Task FlashAsync(
        ToolkitRequest request,
        FlashInput? flashInput,
        RockchipDevice device,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        Func<long, long, ValueTask> progress,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        if (flashInput is null) {
            throw new InvalidOperationException(
                "The flash source was not prepared.");
        }

        async ValueTask Report(string message) =>
            await ReportStatusAsync(report, message)
                .ConfigureAwait(false);
        if (flashInput.SourceKind ==
            FlashSourceKind.UpdateImage) {
            await UpdateImageFlasher.FlashAsync(
                    device,
                    flashInput.Location,
                    logger,
                    Report,
                    progress,
                    limits,
                    cancellationToken)
                .ConfigureAwait(false);
        } else {
            await DirectoryFlasher.FlashAsync(
                    device,
                    flashInput.Location,
                    logger,
                    Report,
                    progress,
                    limits,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.ResetAfterFlash) {
            await Report("Resetting device")
                .ConfigureAwait(false);
            await device.ResetAsync(ResetMode.Normal)
                .ConfigureAwait(false);
            await device.DisposeAsync()
                .ConfigureAwait(false);
            await Report("Device reset")
                .ConfigureAwait(false);
        }

        await ReportSuccessAsync(report, "Flash complete")
            .ConfigureAwait(false);
    }

    private static async Task ReadAsync(
        ToolkitRequest request,
        RockchipDevice device,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.File)) {
            throw new InvalidDataException(
                "The read request contains no output destination.");
        }

        var data = await device.ReadAsync(
                request.Sector ?? 0,
                request.Count ?? 1,
                cancellationToken)
            .ConfigureAwait(false);
        await ToolkitOutput.Create(request.File)
            .WriteAsync(data, logger, cancellationToken)
            .ConfigureAwait(false);
        await ReportSuccessAsync(
                report,
                $"Read {data.Length:N0} bytes")
            .ConfigureAwait(false);
    }

    private static async Task WriteAsync(
        ToolkitRequest request,
        RockchipDevice device,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        Func<long, long, ValueTask> progress,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.File)) {
            throw new InvalidDataException(
                "The write request contains no input file.");
        }

        await using var input = await InputFile.OpenAsync(
                request.File,
                logger,
                message => ReportStatusAsync(report, message),
                progress,
                cancellationToken,
                limits: limits)
            .ConfigureAwait(false);
        await FirmwareWriter.WriteAsync(
                device,
                input.Path,
                request.Sector ?? 0,
                request.Backup,
                request.EraseBeforeWrite,
                logger,
                message => ReportStatusAsync(report, message),
                progress,
                request.WriteLength,
                request.WriteSourceOffset)
            .ConfigureAwait(false);
        await ReportSuccessAsync(report, "Write complete")
            .ConfigureAwait(false);
    }

    private static ResetMode ToResetMode(
        ToolkitResetMode? mode) {
        return mode switch {
            ToolkitResetMode.ResetMassStorage =>
                ResetMode.ResetMassStorage,
            ToolkitResetMode.PowerOff => ResetMode.PowerOff,
            ToolkitResetMode.ResetMaskRom =>
                ResetMode.ResetMaskRom,
            ToolkitResetMode.Disconnect =>
                ResetMode.Disconnect,
            _ => ResetMode.Normal
        };
    }

    private static StorageType ToStorageType(
        ToolkitStorage? storage) {
        return storage switch {
            ToolkitStorage.SdCard => StorageType.SdCard,
            ToolkitStorage.SpiNor => StorageType.SpiNor,
            _ => StorageType.Emmc
        };
    }

    private static ValueTask ReportAsync(
        Func<ToolkitEvent, ValueTask> report,
        string message) {
        return report(new ToolkitEvent("stdout", message));
    }

    private static ValueTask ReportProgress(
        Func<ToolkitEvent, ValueTask> report,
        string label,
        long current,
        long total) {
        return report(new ToolkitEvent(
            "progress",
            label,
            current,
            total));
    }

    private static ValueTask ReportStatusAsync(
        Func<ToolkitEvent, ValueTask> report,
        string message) =>
        report(new ToolkitEvent("status", message));

    private static ValueTask ReportSuccessAsync(
        Func<ToolkitEvent, ValueTask> report,
        string message) =>
        report(new ToolkitEvent("success", message));
}
