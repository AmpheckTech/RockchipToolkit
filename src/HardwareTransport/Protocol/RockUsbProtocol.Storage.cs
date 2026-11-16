namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

public sealed partial class RockUsbProtocol {
    public async ValueTask<byte[]> ReadLbaAsync(
        uint startSector,
        ushort sectorCount,
        ReadWriteMode mode = ReadWriteMode.Image,
        CancellationToken cancellationToken = default
    ) {
        RockUsbCommandPolicy.ValidateSectorCount(sectorCount);
        RockUsbCommandPolicy.ValidateSectorRange(
            startSector,
            sectorCount);

        var data = new byte[checked(sectorCount * SectorSize)];
        await ExecuteAsync(
            RockUsbOperation.ReadLba,
            startSector,
            sectorCount,
            mode,
            data,
            true,
            cancellationToken,
            data).ConfigureAwait(false);
        return data;
    }

    public async ValueTask WriteLbaAsync(
        uint startSector,
        ReadOnlyMemory<byte> data,
        ReadWriteMode mode = ReadWriteMode.Image,
        CancellationToken cancellationToken = default
    ) {
        if (data.Length == 0 ||
            data.Length % SectorSize != 0 ||
            data.Length / SectorSize > MaxSectorsPerTransfer) {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                $"Data must contain 1..{MaxSectorsPerTransfer} complete sectors.");
        }

        var sectorCount =
            checked((ushort)(data.Length / SectorSize));
        RockUsbCommandPolicy.ValidateSectorRange(
            startSector,
            sectorCount);
        await ExecuteAsync(
            RockUsbOperation.WriteLba,
            startSector,
            sectorCount,
            mode,
            data,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask EraseLbaAsync(
        uint startSector,
        ushort sectorCount,
        CancellationToken cancellationToken = default
    ) {
        RockUsbCommandPolicy.ValidateSectorCount(sectorCount);
        RockUsbCommandPolicy.ValidateSectorRange(
            startSector,
            sectorCount);
        await ExecuteAsync(
            RockUsbOperation.EraseLba,
            startSector,
            sectorCount,
            ReadWriteMode.Image,
            ReadOnlyMemory<byte>.Empty,
            false,
            cancellationToken).ConfigureAwait(false);
    }

}
