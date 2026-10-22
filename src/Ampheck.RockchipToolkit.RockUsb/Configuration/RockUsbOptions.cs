namespace Ampheck.RockchipToolkit.RockUsb.Configuration;

/// <summary>Options for the Rockchip DriverAssistant transport.</summary>
public sealed class RockUsbOptions {
    public ushort? VendorId { get; init; } = 0x2207;
    public ushort? ProductId { get; init; }
    public uint ControlDeviceAccess { get; init; } = 0xC0000000;
    public uint ControlDeviceShareMode { get; init; } = 3;
    /// <summary>
    /// Resets the driver pipe before the first command. DriverAssistant
    /// normally does this only when retrying.
    /// </summary>
    public bool ResetPipeOnOpen { get; init; }
    public byte ResetPipeValue { get; init; } = 2;
}
