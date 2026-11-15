namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record AdbDeviceDescriptor {
    public required string SerialNumber { get; init; }
    public required string State { get; init; }
    public string? Product { get; init; }
    public string? Model { get; init; }
    public string? DeviceName { get; init; }
}

public sealed record AdbDevicesParams {
    public string? SerialNumber { get; init; }
}

public sealed record AdbDevicesResult {
    public required AdbDeviceDescriptor[] Devices { get; init; }
}

public sealed record AdbShellParams {
    public string? SerialNumber { get; init; }
    public required string Command { get; init; }
}

public sealed record AdbPushParams {
    public string? SerialNumber { get; init; }
    public string? BlobId { get; init; }
    public string? ServerPath { get; init; }
    public required string Destination { get; init; }
}

public sealed record AdbPullParams {
    public string? SerialNumber { get; init; }
    public required string Source { get; init; }
    public string? DestinationServerPath { get; init; }
}

public sealed record AdbRebootParams {
    public string? SerialNumber { get; init; }
    public ToolkitAdbRebootMode Mode { get; init; } =
        ToolkitAdbRebootMode.System;
}

public sealed record AdbShellSessionParams {
    public string? SerialNumber { get; init; }
}

public enum ToolkitAdbRebootMode {
    System,
    Recovery,
    Bootloader
}
