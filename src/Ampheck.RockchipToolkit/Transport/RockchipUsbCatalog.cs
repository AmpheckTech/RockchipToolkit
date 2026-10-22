namespace Ampheck.RockchipToolkit.Transport;

public static class RockchipUsbCatalog {
    public const ushort DefaultVendorId = 0x2207;
    private static readonly IReadOnlyDictionary<
        (ushort VendorId, ushort ProductId),
        (string Family, string Description, bool IsRockUsb)> Devices =
        new Dictionary<
            (ushort VendorId, ushort ProductId),
            (string Family, string Description, bool IsRockUsb)> {
            [(0x071B, 0x3201)] = ("RK27xx", "Legacy RockUSB device", true),
            [(0x071B, 0x3228)] = ("RK28xx", "Legacy RockUSB device", true),
            [(0x071B, 0x3226)] = ("RKNANO", "Legacy RockUSB device", true),
            [(0x2207, 0x261A)] = ("RKCrown", "Legacy RockUSB device", true),
            [(0x2207, 0x281A)] = ("RK281x", "Legacy RockUSB device", true),
            [(0x2207, 0x273A)] = ("RKCayman", "Legacy RockUSB device", true),
            [(0x2207, 0x290A)] = ("RK29xx", "Legacy RockUSB device", true),
            [(0x2207, 0x282B)] = ("RKPanda", "Legacy RockUSB device", true),
            [(0x2207, 0x262C)] = ("RKSmart", "Legacy RockUSB device", true),
            [(0x2207, 0x292A)] = ("RK292x", "Legacy RockUSB device", true),
            [(0x2207, 0x300A)] = ("RK30xx", "RockUSB device", true),
            [(0x2207, 0x300B)] = ("RK30B", "RockUSB device", true),
            [(0x2207, 0x310B)] = ("RK31xx", "RockUSB device", true),
            [(0x2207, 0x310C)] = ("RK31xx", "RockUSB device", true),
            [(0x2207, 0x320A)] = ("RK32xx", "RockUSB device", true),
            [(0x2207, 0x330A)] = ("RK330x", "RockUSB device", true),
            [(0x2207, 0x350A)] =
                ("RK356x", "RockUSB download gadget", true),
            [(0x2207, 0x0006)] =
                ("Rockchip", "Android ADB gadget", false)
        };

    public static RockchipUsbIdentity? Resolve(
        ushort vendorId,
        ushort productId) {
        if (Devices.TryGetValue(
                (vendorId, productId),
                out var known)) {
            return new RockchipUsbIdentity(
                vendorId,
                productId,
                known.Family,
                known.Description,
                known.IsRockUsb);
        }

        return IsRockUsbDevice(vendorId, productId)
            ? new RockchipUsbIdentity(
                vendorId,
                productId,
                "Rockchip",
                "Rockchip USB device",
                IsRockUsb: true)
            : null;
    }

    public static bool IsRockUsbDevice(
        ushort vendorId,
        ushort productId) {
        if (Devices.TryGetValue(
                (vendorId, productId),
                out var known)) {
            return known.IsRockUsb;
        }

        return vendorId == DefaultVendorId &&
            productId >> 8 != 0;
    }
}
