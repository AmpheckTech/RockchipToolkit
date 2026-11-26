using Ampheck.RockchipToolkit.Images.Rkaf;
using Ampheck.RockchipToolkit.Images.Update;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal static class FirmwareImageValidator {
    public const uint BootloaderSector = 0x40;

    public static bool IsUpdateImage(string path) {
        try {
            using var image = RockchipUpdateImage.Open(path);
            return true;
        } catch (InvalidDataException) {
            return false;
        }
    }

    public static void Validate(
        RockchipPartition partition,
        string imageName,
        long imageLength
    ) {
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

    public static void ValidateBootloader(
        string imageName,
        long imageLength,
        uint firstPartitionSector
    ) {
        if (imageLength <= 0 ||
            imageLength % RockUsbProtocol.SectorSize != 0) {
            throw new InvalidDataException(
                $"{imageName} must contain complete 512-byte sectors.");
        }

        if (firstPartitionSector <= BootloaderSector) {
            throw new InvalidDataException(
                "The parameter file has no reserved bootloader area.");
        }

        var imageSectors =
            (ulong)imageLength / RockUsbProtocol.SectorSize;
        var availableSectors =
            (ulong)firstPartitionSector - BootloaderSector;
        if (imageSectors > availableSectors) {
            throw new InvalidDataException(
                $"{imageName} does not fit the reserved bootloader area " +
                $"before sector 0x{firstPartitionSector:x}.");
        }
    }
}
