using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Ampheck.RockchipToolkit.Images.Common;
using Ampheck.RockchipToolkit.Images.Rkfw;

namespace Ampheck.RockchipToolkit.Images.Update;

internal static class RockchipImageChecksum {
    private const int BufferSize = 4 * 1024 * 1024;
    private const int Md5Length = 32;

    public static async Task ValidateAsync(
        string path,
        RockchipUpdateImage update,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        await using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await ValidateRkafChecksumAsync(
                input,
                update,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        if (update.Kind == RockchipUpdateImageKind.Rkfw) {
            await ValidateRkfwMd5Async(
                    input,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task ValidateRkafChecksumAsync(
        Stream input,
        RockchipUpdateImage update,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        var firmwareOffset = update.Kind == RockchipUpdateImageKind.Rkaf
            ? 0
            : update.ContainerHeader?.FirmwareOffset ??
                throw new InvalidDataException(
                    "The RKFW image has no firmware header.");
        var firmwareSize = update.Kind == RockchipUpdateImageKind.Rkaf
            ? input.Length
            : update.ContainerHeader!.FirmwareSize;
        var checksumOffset = checked(
            firmwareOffset +
            firmwareSize -
            RockchipImageLayout.RkafChecksumSize);
        var storedChecksum = new byte[RockchipImageLayout.RkafChecksumSize];
        input.Position = checksumOffset;
        await StreamRange.ReadExactlyAsync(
                input,
                storedChecksum,
                cancellationToken)
            .ConfigureAwait(false);

        var checksumLength = firmwareSize -
            RockchipImageLayout.RkafChecksumSize;
        progress.AddTotal(checksumLength);
        var checksum = await RockchipCrc32.ComputeAsync(
                input,
                firmwareOffset,
                checksumLength,
                progress.Advance,
                cancellationToken)
            .ConfigureAwait(false);
        if (BinaryPrimitives.ReadUInt32LittleEndian(storedChecksum) !=
            checksum) {
            throw new InvalidDataException(
                "The RKAF trailer checksum is invalid.");
        }
    }

    private static async Task ValidateRkfwMd5Async(
        Stream input,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        if (!RockchipImageReader.HasTrailingMd5(input)) {
            throw new InvalidDataException(
                "The RKFW image has no MD5 trailer.");
        }

        var storedBytes = new byte[Md5Length];
        StreamRange.ReadExactly(
            input,
            storedBytes,
            input.Length - Md5Length);
        progress.AddTotal(input.Length - Md5Length);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = new byte[BufferSize];
        var remaining = input.Length - Md5Length;
        input.Position = 0;
        while (remaining > 0) {
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = await input.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException(
                    "The Rockchip image ended unexpectedly.");
            }

            md5.AppendData(buffer, 0, read);
            progress.Advance(read);
            remaining -= read;
        }

        var stored = Encoding.ASCII.GetString(storedBytes);
        var actual = Convert.ToHexStringLower(md5.GetHashAndReset());
        if (!stored.Equals(actual, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException(
                "The RKFW trailer MD5 is invalid.");
        }
    }
}
