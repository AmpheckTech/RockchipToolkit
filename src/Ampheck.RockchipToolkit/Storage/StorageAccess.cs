using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Firmware.Internal;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Storage;

public sealed partial class StorageAccess {
    private readonly DeviceAccess _access;

    public StorageAccess(IUsbDeviceProviderFactory hardware) {
        ArgumentNullException.ThrowIfNull(hardware);
        var connector = new DeviceConnector(hardware);
        _access = new DeviceAccess(
            new RockchipBootLoader(),
            connector);
    }

    private static void ValidateWrite(
        long inputLength,
        long sourceOffset,
        long length
    ) {
        if (sourceOffset < 0 ||
            length <= 0 ||
            sourceOffset > inputLength ||
            length > inputLength - sourceOffset) {
            throw new InvalidDataException(
                "The write range is outside the input file.");
        }
    }
}
