namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class CommandNames {
    public const string Devices = "devices";
    public const string Info = "info";
    public const string ChipInfo = "chip info";
    public const string ChipId = "chip id";
    public const string Capability = "capability";
    public const string Loader = "loader";
    public const string Flash = "flash";
    public const string Read = "read";
    public const string Write = "write";
    public const string Erase = "erase";
    public const string Storage = "storage";
    public const string Reset = "reset";
    public const string Server = "server";
    public const string ServerNodaemon = "server nodaemon";
    public const string Discover = "discover";
    public const string Adb = "adb";
    public const string AdbPrefix = Adb + " ";
    public const string AdbDevices = Adb + " devices";
    public const string AdbShell = Adb + " shell";
    public const string AdbPush = Adb + " push";
    public const string AdbPull = Adb + " pull";
    public const string AdbRebootLoader = Adb + " reboot loader";
    public const string Completion = "completion";
    public const string Serial = "serial";
    public const string SerialPrefix = Serial + " ";
    public const string SerialList = Serial + " list";
    public const string SerialOpen = Serial + " open";
    public const string SerialClose = Serial + " close";
    public const string SerialTerminal = Serial + " terminal";
    public const string SerialHistory = Serial + " history";
}
