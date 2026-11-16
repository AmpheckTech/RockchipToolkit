namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

public enum RockUsbOperation : byte {
    ReadFlashId = 0x01,
    ReadLba = 0x14,
    WriteLba = 0x15,
    ReadFlashInfo = 0x1A,
    ReadChipInfo = 0x1B,
    EraseLba = 0x25,
    ChangeStorage = 0x2A,
    ReadCapability = 0xAA,
    DeviceReset = 0xFF
}
