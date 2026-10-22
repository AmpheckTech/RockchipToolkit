namespace Ampheck.Adb.Cli;

internal static class DeviceSelection {
    public static Task<AdbDevice> RequireOnlineAsync(
        AdbClient client,
        ClientSettings settings,
        CancellationToken cancellationToken) {
        return client.GetOnlineDeviceAsync(
            settings.SerialNumber,
            cancellationToken);
    }
}
