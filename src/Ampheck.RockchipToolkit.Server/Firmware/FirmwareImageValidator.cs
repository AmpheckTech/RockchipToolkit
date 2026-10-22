using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Protocol;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal static class FirmwareImageValidator {
    public static void Validate(
        RockchipPartition partition,
        string imageName,
        long imageLength) {
        if (imageLength <= 0 ||
            imageLength % RockUsbProtocol.SectorSize != 0) {
            throw new InvalidDataException(
                $"{imageName} must contain complete 512-byte sectors.");
        }

        if (partition.Size is { } size) {
            var imageSectors =
                (ulong)imageLength / RockUsbProtocol.SectorSize;
            if (imageSectors > size) {
                throw new InvalidDataException(
                    $"{imageName} does not fit partition " +
                    $"{partition.Name} ({size} sectors).");
            }
        }
    }
}
