using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Cli.Assets.Services;

internal static class AssetHardLink {
    public static bool TryCreate(string source, string destination) {
        try {
            if (OperatingSystem.IsWindows()) {
                return CreateHardLink(
                    destination,
                    source,
                    IntPtr.Zero);
            }

            return OperatingSystem.IsMacOS()
                ? MacLink(source, destination) == 0
                : LinuxLink(source, destination) == 0;
        } catch (Exception error) when (
            error is DllNotFoundException or EntryPointNotFoundException) {
            return false;
        }
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int LinuxLink(
        string source,
        string destination
    );

    [DllImport("libSystem.B.dylib", EntryPoint = "link", SetLastError = true)]
    private static extern int MacLink(
        string source,
        string destination
    );

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes
    );
}
