using System.Net.WebSockets;

namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed partial class ToolkitRpcClient {
    public ValueTask<ServerInfoResult> GetServerInfoAsync(
        CancellationToken cancellationToken = default) =>
        RequestAsync<EmptyParams, ServerInfoResult>(
            JsonRpcMethods.ServerInfo,
            new EmptyParams(),
            cancellationToken: cancellationToken);

    public ValueTask<DeviceListResult> ListDevicesAsync(
        DeviceListParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceListParams, DeviceListResult>(
            JsonRpcMethods.DeviceList,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<DeviceInfoResult> GetDeviceInfoAsync(
        DeviceInfoParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceInfoParams, DeviceInfoResult>(
            JsonRpcMethods.DeviceInfo,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<DeviceChipInfoResult> GetChipInfoAsync(
        DeviceChipInfoParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceChipInfoParams, DeviceChipInfoResult>(
            JsonRpcMethods.DeviceChipInfo,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<DeviceChipIdResult> GetChipIdAsync(
        DeviceChipIdParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceChipIdParams, DeviceChipIdResult>(
            JsonRpcMethods.DeviceChipId,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<DeviceCapabilityResult> GetCapabilitiesAsync(
        DeviceCapabilityParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceCapabilityParams, DeviceCapabilityResult>(
            JsonRpcMethods.DeviceCapability,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> LoadLoaderAsync(
        DeviceLoaderParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceLoaderParams, EmptyResult>(
            JsonRpcMethods.DeviceLoader,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> ResetDeviceAsync(
        DeviceResetParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<DeviceResetParams, EmptyResult>(
            JsonRpcMethods.DeviceReset,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<EmptyResult> FlashFirmwareAsync(
        FirmwareFlashParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<FirmwareFlashParams, EmptyResult>(
            JsonRpcMethods.FirmwareFlash,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<FileReference> ReadStorageAsync(
        StorageReadParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<StorageReadParams, FileReference>(
            JsonRpcMethods.StorageRead,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<StorageWriteResult> WriteStorageAsync(
        StorageWriteParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<StorageWriteParams, StorageWriteResult>(
            JsonRpcMethods.StorageWrite,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> EraseStorageAsync(
        StorageEraseParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<StorageEraseParams, EmptyResult>(
            JsonRpcMethods.StorageErase,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> SelectStorageAsync(
        StorageSelectParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<StorageSelectParams, EmptyResult>(
            JsonRpcMethods.StorageSelect,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<SerialListResult> ListSerialPortsAsync(
        CancellationToken cancellationToken = default) =>
        RequestAsync<EmptyParams, SerialListResult>(
            JsonRpcMethods.SerialList,
            new EmptyParams(),
            cancellationToken: cancellationToken);

    public ValueTask<EmptyResult> OpenSerialPortAsync(
        SerialOpenParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<SerialOpenParams, EmptyResult>(
            JsonRpcMethods.SerialOpen,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<SerialCloseResult> CloseSerialPortAsync(
        SerialCloseParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<SerialCloseParams, SerialCloseResult>(
            JsonRpcMethods.SerialClose,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<SerialClearResult> ClearSerialBufferAsync(
        SerialPortParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<SerialPortParams, SerialClearResult>(
            JsonRpcMethods.SerialClear,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<FileReference> ReadSerialHistoryAsync(
        SerialHistoryParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<SerialHistoryParams, FileReference>(
            JsonRpcMethods.SerialHistory,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<ClientWebSocket> OpenSerialTerminalAsync(
        SerialTerminalParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        OpenStreamAsync(
            JsonRpcMethods.SerialTerminal,
            parameters,
            "/api/serial",
            "arkt.serial.v1",
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> EnsureAdbServerAsync(
        CancellationToken cancellationToken = default) =>
        RequestAsync<EmptyParams, EmptyResult>(
            JsonRpcMethods.AdbServerEnsure,
            new EmptyParams(),
            cancellationToken: cancellationToken);

    public ValueTask<AdbDevicesResult> ListAdbDevicesAsync(
        AdbDevicesParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<AdbDevicesParams, AdbDevicesResult>(
            JsonRpcMethods.AdbDevices,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<TextResult> ExecuteAdbShellAsync(
        AdbShellParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<AdbShellParams, TextResult>(
            JsonRpcMethods.AdbShell,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> PushAdbFileAsync(
        AdbPushParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<AdbPushParams, EmptyResult>(
            JsonRpcMethods.AdbPush,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<FileReference> PullAdbFileAsync(
        AdbPullParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        RequestAsync<AdbPullParams, FileReference>(
            JsonRpcMethods.AdbPull,
            parameters,
            onEvent,
            cancellationToken);

    public ValueTask<EmptyResult> RebootAdbDeviceAsync(
        AdbRebootParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<AdbRebootParams, EmptyResult>(
            JsonRpcMethods.AdbReboot,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<ClientWebSocket> OpenAdbShellAsync(
        AdbShellSessionParams parameters,
        Action<TaskEventNotification>? onEvent = null,
        CancellationToken cancellationToken = default) =>
        OpenStreamAsync(
            JsonRpcMethods.AdbShellSession,
            parameters,
            "/api/adb",
            "arkt.adb.v1",
            onEvent,
            cancellationToken);

    public ValueTask<TaskListResult> ListTasksAsync(
        TaskListParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<TaskListParams, TaskListResult>(
            JsonRpcMethods.TaskList,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<TaskInfo> GetTaskAsync(
        TaskGetParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<TaskGetParams, TaskInfo>(
            JsonRpcMethods.TaskGet,
            parameters,
            cancellationToken: cancellationToken);

    public ValueTask<TaskCancelResult> CancelTaskAsync(
        TaskCancelParams parameters,
        CancellationToken cancellationToken = default) =>
        RequestAsync<TaskCancelParams, TaskCancelResult>(
            JsonRpcMethods.TaskCancel,
            parameters,
            cancellationToken: cancellationToken);
}
