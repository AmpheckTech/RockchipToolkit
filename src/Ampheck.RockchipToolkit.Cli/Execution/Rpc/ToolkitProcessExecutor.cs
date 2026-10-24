using System.Diagnostics;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class ToolkitProcessExecutor : IToolkitExecutor {
    private readonly ToolkitTransferLimits _limits;

    public ToolkitProcessExecutor(
        ToolkitTransferLimits? limits = null) {
        _limits = limits ?? ToolkitTransferLimits.Default;
        _limits.Validate();
    }

    public async Task<ToolkitResult> ExecuteAsync(ToolkitRequest request, Func<ToolkitEvent, ValueTask> report, CancellationToken cancellationToken) {
        var startInfo = ToolkitWorkerProcessFactory.Create(
            request,
            _limits);
        using var process = new Process {
            StartInfo = startInfo
        };
        if (!process.Start()) {
            throw new InvalidOperationException(
                "Unable to start the toolkit operation process.");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string? lastError = null;
        var outputTask = PumpAsync(
            process.StandardOutput,
            "stdout",
            report,
            stop,
            onErrorLine: null);
        var errorTask = PumpAsync(
            process.StandardError,
            "stderr",
            report,
            stop,
            line => Volatile.Write(ref lastError, line));
        try {
            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            return new ToolkitResult(
                process.ExitCode == 0,
                process.ExitCode,
                process.ExitCode == 0
                    ? null
                    : Volatile.Read(ref lastError) ??
                        $"Toolkit worker exited with code " +
                        $"{process.ExitCode}.");
        } catch (OperationCanceledException) {
            TryKill(process);
            throw;
        } finally {
            if (!process.HasExited) {
                TryKill(process);
            }
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        string source,
        Func<ToolkitEvent, ValueTask> report,
        CancellationTokenSource stop,
        Action<string>? onErrorLine) {
        try {
            while (await reader.ReadLineAsync(stop.Token).ConfigureAwait(false) is { } line) {
                var value = source == "stdout" &&
                    ToolkitWorkerEvent.TryParse(line, out var progress)
                        ? progress
                        : new ToolkitEvent(source, line);
                await report(value).ConfigureAwait(false);
                if (source == "stderr") {
                    onErrorLine?.Invoke(line);
                }
            }
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
        } catch {
            await stop.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void TryKill(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (Exception error) when (
            error is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException) {
        }
    }
}
