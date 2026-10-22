using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class UnixTerminalMode : IDisposable {
    private const int StandardInput = 0;
    private const int SetNow = 0;
    private readonly UnixTerminalState? _savedState;

    private UnixTerminalMode(
        UnixTerminalState? savedState) {
        _savedState = savedState;
    }

    public static ValueTask<UnixTerminalMode> EnterAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows() ||
            TcGetAttributes(
                StandardInput,
                out var saved) != 0) {
            return ValueTask.FromResult(
                new UnixTerminalMode(savedState: null));
        }

        var raw = saved;
        CfMakeRaw(ref raw);
        if (TcSetAttributes(
                StandardInput,
                SetNow,
                ref raw) != 0) {
            return ValueTask.FromResult(
                new UnixTerminalMode(savedState: null));
        }

        return ValueTask.FromResult(
            new UnixTerminalMode(saved));
    }

    public void Dispose() {
        if (_savedState is not { } saved) {
            return;
        }

        TcSetAttributes(
            StandardInput,
            SetNow,
            ref saved);
    }

    [DllImport(
        "libc",
        EntryPoint = "tcgetattr",
        SetLastError = true)]
    private static extern int TcGetAttributes(
        int descriptor,
        out UnixTerminalState state);

    [DllImport(
        "libc",
        EntryPoint = "tcsetattr",
        SetLastError = true)]
    private static extern int TcSetAttributes(
        int descriptor,
        int action,
        ref UnixTerminalState state);

    [DllImport(
        "libc",
        EntryPoint = "cfmakeraw")]
    private static extern void CfMakeRaw(
        ref UnixTerminalState state);
}
