namespace Ampheck.RockchipToolkit.Core.Operations;

public static class OperationProgressExtensions {
    public static void ReportStatus(
        this IProgress<OperationProgress>? progress,
        string message
    ) {
        progress?.Report(new OperationProgress {
            Kind = "status",
            Message = message
        });
    }

    public static void ReportOutput(
        this IProgress<OperationProgress>? progress,
        string message
    ) {
        progress?.Report(new OperationProgress {
            Kind = "stdout",
            Message = message
        });
    }

    public static void ReportProgress(
        this IProgress<OperationProgress>? progress,
        string label,
        long current,
        long total
    ) {
        progress?.Report(new OperationProgress {
            Kind = "progress",
            Message = label,
            Progress = new TaskProgress {
                Current = current,
                Total = total
            }
        });
    }
}
