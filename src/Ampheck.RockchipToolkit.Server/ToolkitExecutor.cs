using Ampheck.RockchipToolkit.Device;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server.Devices;
using Ampheck.RockchipToolkit.Server.Firmware;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

public sealed class ToolkitExecutor(
    ILoggerFactory loggerFactory,
    ToolkitTransferLimits? limits = null) : IToolkitExecutor {
    private readonly ILoggerFactory _loggerFactory =
        loggerFactory ?? throw new ArgumentNullException(
            nameof(loggerFactory));
    private readonly ToolkitTransferLimits _limits =
        limits ?? ToolkitTransferLimits.Default;

    public async Task<ToolkitResult> ExecuteAsync(
        ToolkitRequest request,
        Func<ToolkitEvent, ValueTask> report,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(report);
        _limits.Validate();
        ValidateRequest(request);
        var logger = _loggerFactory.CreateLogger<ToolkitExecutor>();
        if (request.Operation == ToolkitOperation.Devices) {
            await ListDevicesAsync(
                    request,
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
            return Success();
        }

        await using var flashInput =
            await OpenFlashInputAsync(
                    request,
                    logger,
                    report,
                    _limits,
                    cancellationToken)
                .ConfigureAwait(false);
        await LoadRequestedLoaderAsync(
                request,
                flashInput,
                logger,
                report,
                cancellationToken)
            .ConfigureAwait(false);
        await using var session = await DeviceConnector.OpenAsync(
                request,
                _loggerFactory,
                logger,
                cancellationToken)
            .ConfigureAwait(false);
        await ToolkitDeviceOperations.ExecuteAsync(
                request,
                flashInput,
                session.Transport,
                _loggerFactory,
                logger,
                report,
                _limits,
                cancellationToken)
            .ConfigureAwait(false);
        return Success();
    }

    private async Task ListDevicesAsync(
        ToolkitRequest request,
        Func<ToolkitEvent, ValueTask> report,
        CancellationToken cancellationToken) {
        var logger = _loggerFactory.CreateLogger<ToolkitExecutor>();
        var devices = await DeviceConnector.ListAsync(
                request,
                _loggerFactory,
                cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Found {DeviceCount} RockUSB device(s)",
            devices.Count);
        foreach (var item in devices) {
            await ReportAsync(
                    report,
                    $"{item.VendorId:X4}:{item.ProductId:X4} " +
                    $"USB {item.UsbVersion:X4} {item.Mode} " +
                    $"{item.PlatformId ?? string.Empty}")
                .ConfigureAwait(false);
        }
    }

    private static async Task<FlashInput?> OpenFlashInputAsync(
        ToolkitRequest request,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken) {
        if (request.Operation != ToolkitOperation.Flash) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Source)) {
            throw new InvalidDataException(
                "The flash request contains no firmware source.");
        }

        return await FlashInput.OpenAsync(
                request.Source,
                logger,
                message => ReportStatusAsync(report, message),
                (current, total) => ReportProgress(
                    report,
                    "firmware",
                    current,
                    total),
                cancellationToken,
                limits: limits)
            .ConfigureAwait(false);
    }

    private async Task LoadRequestedLoaderAsync(
        ToolkitRequest request,
        FlashInput? flashInput,
        ILogger logger,
        Func<ToolkitEvent, ValueTask> report,
        CancellationToken cancellationToken) {
        var loaderLocation = request.Loader;
        if (loaderLocation is null &&
            flashInput?.SourceKind ==
                FlashSourceKind.UpdateImage &&
            await DeviceConnector.GetModeAsync(
                    request,
                    _loggerFactory,
                    cancellationToken)
                .ConfigureAwait(false) ==
                RockchipUsbMode.MaskRom) {
            loaderLocation = flashInput.Location;
            logger.LogInformation(
                "MaskROM detected; using the update image's " +
                "embedded loader");
        }

        if (loaderLocation is null) {
            return;
        }

        await using var loader = await InputFile.OpenAsync(
                loaderLocation,
                logger,
                message => ReportStatusAsync(report, message),
                (current, total) => ReportProgress(
                    report,
                    "loader",
                    current,
                    total),
                cancellationToken,
                limits: _limits)
            .ConfigureAwait(false);
        await using var loaderSession =
            await DeviceConnector.OpenAsync(
                    request,
                    _loggerFactory,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);
        await using var loaderDevice = new RockchipDevice(
            loaderSession.Transport,
            loggerFactory: _loggerFactory);
        await loaderDevice.DownloadBootAsync(
                loader.Path,
                cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Loader prepared for command execution");
        await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken)
            .ConfigureAwait(false);
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

    private static void ValidateRequest(ToolkitRequest request) {
        switch (request.Operation) {
            case ToolkitOperation.Erase when
                request.Sector is null ||
                request.Count is null or 0:
                throw new InvalidDataException(
                    "The erase operation requires a sector and count.");
            case ToolkitOperation.Read when request.Count == 0:
                throw new InvalidDataException(
                    "The read count must be greater than zero.");
            case ToolkitOperation.Write when
                request.Sector is null ||
                request.WriteLength == 0 ||
                request.WriteSourceOffset is < 0:
                throw new InvalidDataException(
                    "The write operation requires a sector, and its " +
                    "length and offset must be valid.");
            case ToolkitOperation.Storage when
                request.Storage is null:
                throw new InvalidDataException(
                    "The storage operation requires a storage type.");
            case ToolkitOperation.Flash when
                string.IsNullOrWhiteSpace(request.Source):
                throw new InvalidDataException(
                    "The flash request contains no firmware source.");
            case ToolkitOperation.Loader when
                string.IsNullOrWhiteSpace(request.File):
                throw new InvalidDataException(
                    "The loader request contains no file.");
        }
    }

    private static ToolkitResult Success() {
        return new ToolkitResult(
            Success: true,
            ExitCode: 0,
            Error: null,
            Output: null);
    }
}
