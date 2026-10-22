namespace Ampheck.RockchipToolkit.Protocol;

/// <summary>Features reported by the RockUSB read-capability command.</summary>
[Flags]
public enum RockchipCapabilityFlags : ulong {
    None = 0,
    DirectLba = 1UL << 0,
    VendorStorage = 1UL << 1,
    First4MiBAccess = 1UL << 2,
    ReadLba = 1UL << 3,
    NewVendorStorage = 1UL << 4,
    ReadUartRam = 1UL << 5,
    ReadIdbConfig = 1UL << 6,
    ReadSecureMode = 1UL << 7,
    NewIdb = 1UL << 8,
    StorageMediaInfo = 1UL << 9,
    LbaWriteParity = 1UL << 10,
    ReadOtpData = 1UL << 11,
    Usb3Download = 1UL << 12,
    WriteOtpProof = 1UL << 13,
    WriteCipherKey = 1UL << 14
}
