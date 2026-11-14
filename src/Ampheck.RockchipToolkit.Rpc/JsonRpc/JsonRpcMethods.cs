namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public static class JsonRpcMethods {
    public const string ServerInfo = "server.info";
    public const string DeviceList = "device.list";
    public const string DeviceInfo = "device.info";
    public const string DeviceChipInfo = "device.chip.info";
    public const string DeviceChipId = "device.chip.id";
    public const string DeviceCapability = "device.capability";
    public const string DeviceLoader = "device.loader";
    public const string DeviceReset = "device.reset";
    public const string FirmwareFlash = "firmware.flash";
    public const string StorageRead = "storage.read";
    public const string StorageWrite = "storage.write";
    public const string StorageErase = "storage.erase";
    public const string StorageSelect = "storage.select";
    public const string SerialList = "serial.list";
    public const string SerialOpen = "serial.open";
    public const string SerialClose = "serial.close";
    public const string SerialClear = "serial.clear";
    public const string SerialHistory = "serial.history";
    public const string SerialTerminal = "serial.terminal";
    public const string AdbServerEnsure = "adb.server.ensure";
    public const string AdbDevices = "adb.devices";
    public const string AdbShell = "adb.shell";
    public const string AdbShellSession = "adb.shell.session";
    public const string AdbPush = "adb.push";
    public const string AdbPull = "adb.pull";
    public const string AdbReboot = "adb.reboot";
    public const string TaskList = "tasks.list";
    public const string TaskGet = "tasks.get";
    public const string TaskCancel = "tasks.cancel";
    public const string TaskSubscribe = "tasks.subscribe";
}
