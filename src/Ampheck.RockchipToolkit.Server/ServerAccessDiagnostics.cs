using System.Runtime.InteropServices;
using Ampheck.Serial;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

internal static class ServerAccessDiagnostics {
    private const int ReadWriteAccess = 6;

    public static void Report(ILogger logger) {
        logger.LogInformation(
            "Server user: {UserName}",
            Environment.UserName);
        if (!OperatingSystem.IsLinux()) {
            var ports = SerialPortDiscovery.List();
            logger.LogInformation(
                "Serial ports: {Ports}",
                ports.Count == 0
                    ? "(none)"
                    : string.Join(", ", ports));
            logger.LogInformation(
                "USB and serial access is verified by the " +
                "platform drivers when a device is opened.");
            return;
        }

        var paths = FindLinuxDevicePaths();
        if (paths.Count == 0) {
            logger.LogInformation(
                "No USB serial or USB device nodes are currently present.");
            return;
        }

        var denied = paths
            .Where(path => Access(
                    path,
                    ReadWriteAccess) != 0)
            .ToArray();
        if (denied.Length == 0) {
            logger.LogInformation(
                "USB and serial device permissions are available.");
            return;
        }

        logger.LogWarning(
            "Current user cannot read/write {DeniedCount} device " +
            "node(s): {Paths}. Configure a udev rule or add the " +
            "user to the device group before using USB or serial.",
            denied.Length,
            string.Join(", ", denied.Take(8)));
    }

    private static IReadOnlyList<string> FindLinuxDevicePaths() {
        var paths = new HashSet<string>(
            SerialPortDiscovery.List(),
            StringComparer.Ordinal);

        if (Directory.Exists("/dev/bus/usb")) {
            try {
                foreach (var bus in Directory.EnumerateDirectories(
                    "/dev/bus/usb")) {
                    AddMatches(paths, bus, "*");
                }
            } catch (Exception error) when (
                error is IOException or UnauthorizedAccessException) {
            }
        }

        return paths.Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddMatches(
        HashSet<string> paths,
        string directory,
        string pattern) {
        try {
            foreach (var path in Directory.EnumerateFiles(
                directory,
                pattern)) {
                paths.Add(path);
            }
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }

    [DllImport(
        "libc",
        EntryPoint = "access",
        SetLastError = true)]
    private static extern int Access(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int mode);
}
