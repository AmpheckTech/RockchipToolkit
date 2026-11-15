using Ampheck.RockchipToolkit.Device;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed class FirmwareFlashContext(
    RockchipDevice device,
    FirmwareFileContext files,
    Func<long, long, ValueTask>? eraseProgress) {
    public RockchipDevice Device { get; } = device;
    public FirmwareFileContext Files { get; } = files;
    public Func<long, long, ValueTask>? EraseProgress { get; } =
        eraseProgress;

    public FirmwareWriter CreateWriter(
        Func<long, long, ValueTask>? progressOverride = null) {
        return new FirmwareWriter(
            Device,
            Files.Logger,
            Files.Report,
            progressOverride ?? Files.Progress,
            EraseProgress);
    }
}
