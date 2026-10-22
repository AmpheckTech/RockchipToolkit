namespace Ampheck.RockchipToolkit.Transport;

/// <summary>Known Rockchip USB modes.</summary>
[Flags]
public enum RockchipUsbMode {
    None = 0,
    MaskRom = 1,
    Loader = 2,
    MassStorage = 4
}
