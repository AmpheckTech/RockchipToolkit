using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Arkt.Terminal;

internal static class ConsoleInput {
    private const int StandardInputHandle = -10;

    public static bool IsInteractiveConsole {
        get {
            if (!Environment.UserInteractive) {
                return false;
            }

            return OperatingSystem.IsWindows()
                ? HasWindowsConsole()
                : !System.Console.IsInputRedirected;
        }
    }

    private static bool HasWindowsConsole() {
        var handle = GetStdHandle(StandardInputHandle);
        return handle != IntPtr.Zero &&
            handle != new IntPtr(-1) &&
            GetConsoleMode(handle, out _);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(
        IntPtr consoleHandle,
        out uint mode
    );
}
