using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class FirmwareFlashContext(
    RockchipDevice device,
    IProgress<OperationProgress>? status,
    Func<long, long, ValueTask>? progress,
    Func<long, long, ValueTask>? eraseProgress
) {
    public RockchipDevice Device { get; } = device;
    public IProgress<OperationProgress>? Status { get; } = status;
    public Func<long, long, ValueTask>? Progress { get; } = progress;
    public Func<long, long, ValueTask>? EraseProgress { get; } =
        eraseProgress;

    public DeviceWriter CreateWriter(
        Func<long, long, ValueTask>? progressOverride = null
    ) {
        return new DeviceWriter(
            Device,
            progressOverride ?? Progress,
            EraseProgress);
    }
}
