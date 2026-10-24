using System.Diagnostics;
using System.Globalization;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal sealed class LocalServerStateStore(string? path) {
    private static readonly long StartTimeToleranceTicks =
        TimeSpan.FromSeconds(2).Ticks;

    public void WriteCurrentProcess() {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        using var process = Process.GetCurrentProcess();
        File.WriteAllLines(
            path,
            [
                process.Id.ToString(
                    CultureInfo.InvariantCulture),
                process.StartTime
                    .ToUniversalTime()
                    .Ticks
                    .ToString(
                        CultureInfo.InvariantCulture)
            ]);
    }

    public Process? FindRunningServer() {
        if (!TryRead(out var state)) {
            return null;
        }

        try {
            var process = Process.GetProcessById(
                state.ProcessId);
            if (!process.HasExited &&
                Math.Abs(
                    process.StartTime
                        .ToUniversalTime()
                        .Ticks -
                    state.StartTimeUtcTicks) <=
                    StartTimeToleranceTicks) {
                return process;
            }

            process.Dispose();
            return null;
        } catch (Exception error) when (
            error is ArgumentException or
            InvalidOperationException or
            System.ComponentModel.Win32Exception) {
            return null;
        }
    }

    public void Delete() {
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or
            UnauthorizedAccessException) {
        }
    }

    private bool TryRead(
        out LocalServerState state) {
        state = default;
        if (string.IsNullOrWhiteSpace(path)) {
            return false;
        }

        try {
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2 ||
                !int.TryParse(
                    lines[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var processId) ||
                !long.TryParse(
                    lines[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var ticks)) {
                return false;
            }

            state = new LocalServerState(
                processId,
                ticks);
            return true;
        } catch (Exception error) when (
            error is IOException or
            UnauthorizedAccessException) {
            return false;
        }
    }
}
