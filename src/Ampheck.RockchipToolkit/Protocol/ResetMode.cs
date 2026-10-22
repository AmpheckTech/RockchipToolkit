namespace Ampheck.RockchipToolkit.Protocol;

public enum ResetMode : byte {
    Normal = 0,
    ResetMassStorage = 1,
    PowerOff = 2,
    ResetMaskRom = 3,
    Disconnect = 4
}
