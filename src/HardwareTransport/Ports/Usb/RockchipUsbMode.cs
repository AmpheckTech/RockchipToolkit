namespace Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;

/// <summary>Known Rockchip USB modes.</summary>
[Flags]
public enum RockchipUsbMode {
    None = 0,
    MaskRom = 1,
    Loader = 2
}
