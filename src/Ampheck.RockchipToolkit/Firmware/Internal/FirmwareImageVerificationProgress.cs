using Ampheck.RockchipToolkit.Images.Common;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class FirmwareImageVerificationProgress(
    IProgress<OperationProgress> progress,
    string item
) : IProgress<RockchipImageProgress> {
    public void Report(RockchipImageProgress value) {
        progress.Report(new OperationProgress(
            ToolkitOperationStage.Verifying,
            item,
            value.Current,
            value.Total));
    }
}
