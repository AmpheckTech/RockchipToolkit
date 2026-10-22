using System.ComponentModel;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class UsbAccessError {
    public static bool IsPermissionFailure(Exception error) {
        for (var current = error;
            current is not null;
            current = current.InnerException) {
            if (current is Win32Exception {
                NativeErrorCode: 5 or 13
            } ||
                current.Message.Contains(
                    "insufficient permissions",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "permission denied",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "LIBUSB_ERROR_ACCESS",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "/dev/bus/usb",
                    StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains(
                    "denied",
                    StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    public static InvalidOperationException Wrap(Exception error) {
        var hint = OperatingSystem.IsWindows()
            ? "Install the Rockchip USB driver and reconnect the device."
            : "Configure a udev rule, join the dialout group, " +
              "or run the command with sudo.";
        return new InvalidOperationException(
            $"{error.Message} {hint}",
            error);
    }
}
