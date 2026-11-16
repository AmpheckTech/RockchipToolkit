using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.Features.Firmware.Model;

namespace Ampheck.RockchipToolkit.Features.Firmware.Services;

internal sealed class FirmwareFlashContext(
    RockchipDevice device,
    InputFileContext files,
    Func<long, long, ValueTask>? eraseProgress
) {
    public RockchipDevice Device { get; } = device;
    public InputFileContext Files { get; } = files;
    public Func<long, long, ValueTask>? EraseProgress { get; } =
        eraseProgress;

    public DeviceWriter CreateWriter(
        Func<long, long, ValueTask>? progressOverride = null
    ) {
        return new DeviceWriter(
            Device,
            Files.Logger,
            Files.Report,
            progressOverride ?? Files.Progress,
            EraseProgress);
    }
}
