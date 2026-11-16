using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Ampheck.RockchipToolkit.Images.Common;
using Ampheck.RockchipToolkit.Images.Rkfw;

namespace Ampheck.RockchipToolkit.Images.Update;

/// <summary>Packs RKFW or standalone RKAF images from an unpacked directory.</summary>
public static class RockchipImagePacker {
    private const int CopyBufferSize = 4 * 1024 * 1024;
    private const int DigestLength = 32;
    private const string BootFileName = "BOOT";

    public static Task PackAsync(
        string inputDirectory,
        string outputPath,
        bool overwrite,
        CancellationToken cancellationToken
    ) {
        return PackAsync(
            inputDirectory,
            outputPath,
            overwrite,
            chip: null,
            progress: null,
            cancellationToken);
    }

    public static Task PackAsync(
        string inputDirectory,
        string outputPath,
        bool overwrite,
        IProgress<RockchipImageProgress>? progress,
        CancellationToken cancellationToken
    ) {
        return PackAsync(
            inputDirectory,
            outputPath,
            overwrite,
            chip: null,
            progress,
            cancellationToken);
    }

    public static Task PackAsync(
        string inputDirectory,
        string outputPath,
        bool overwrite,
        string? chip,
        CancellationToken cancellationToken
    ) {
        return PackAsync(
            inputDirectory,
            outputPath,
            overwrite,
            chip,
            progress: null,
            cancellationToken);
    }

    public static async Task PackAsync(
        string inputDirectory,
        string outputPath,
        bool overwrite,
        string? chip,
        IProgress<RockchipImageProgress>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var input = Path.GetFullPath(inputDirectory);
        var output = Path.GetFullPath(outputPath);
        if (!Directory.Exists(input)) {
            throw new DirectoryNotFoundException(
                $"Input directory '{input}' does not exist.");
        }

        if (Directory.Exists(output)) {
            throw new IOException(
                $"Output '{output}' is a directory.");
        }

        if (File.Exists(output) && !overwrite) {
            throw new IOException(
                $"Output '{output}' already exists. Use --force to replace it.");
        }

        var parent = Path.GetDirectoryName(output) ??
            throw new ArgumentException(
                "The output path has no parent directory.",
                nameof(outputPath));
        Directory.CreateDirectory(parent);
        var temporaryOutput = Path.Combine(
            parent,
            $".{Path.GetFileName(output)}.tmp-{Guid.NewGuid():N}");
        var reporter = new RockchipImageProgressReporter(progress);
        var hasBoot = File.Exists(Path.Combine(input, BootFileName));

        try {
            if (hasBoot) {
                if (string.IsNullOrWhiteSpace(chip)) {
                    throw new InvalidDataException(
                        "Packing RKFW requires --chip.");
                }

                await PackRkfwAsync(
                        input,
                        temporaryOutput,
                        chip,
                        reporter,
                        cancellationToken)
                    .ConfigureAwait(false);
            } else {
                if (!string.IsNullOrWhiteSpace(chip)) {
                    throw new InvalidDataException(
                        "--chip is only valid when the input contains BOOT.");
                }

                await RockchipUpdatePackage.WriteAsync(
                        input,
                        temporaryOutput,
                        reporter,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(
                temporaryOutput,
                output,
                overwrite);
            reporter.Complete();
        } catch {
            if (File.Exists(temporaryOutput)) {
                File.Delete(temporaryOutput);
            }

            throw;
        }
    }

    private static async Task PackRkfwAsync(
        string inputDirectory,
        string outputPath,
        string chip,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        var bootPath = Path.Combine(inputDirectory, BootFileName);
        long bootSize;
        using (var boot = RockchipImage.Open(bootPath)) {
            if (!boot.Header.Kind.IsStandaloneBootImage()) {
                throw new InvalidDataException(
                    "BOOT is not a Rockchip boot image.");
            }

            bootSize = boot.Header.BootSize;
        }

        if (bootSize == 0) {
            throw new InvalidDataException("BOOT is empty.");
        }

        var updatePath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            $".rkaf.tmp-{Guid.NewGuid():N}");
        try {
            await RockchipUpdatePackage.WriteAsync(
                    inputDirectory,
                    updatePath,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            var updateSize = new FileInfo(updatePath).Length;
            var firmwareVersion = await ReadFirmwareVersionAsync(
                    updatePath,
                    cancellationToken)
                .ConfigureAwait(false);
            var header = RockchipRkfwHeader.Create(
                chip,
                firmwareVersion,
                bootSize,
                updateSize);
            await WriteRkfwAsync(
                    outputPath,
                    header,
                    bootPath,
                    bootSize,
                    updatePath,
                    updateSize,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            if (File.Exists(updatePath)) {
                File.Delete(updatePath);
            }
        }
    }

    private static async Task<uint> ReadFirmwareVersionAsync(
        string updatePath,
        CancellationToken cancellationToken
    ) {
        var header = new byte[
            RockchipImageLayout.RkafVersionOffset + sizeof(uint)];
        await using var update = new FileStream(
            updatePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await StreamRange.ReadExactlyAsync(
                update,
                header,
                cancellationToken)
            .ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(RockchipImageLayout.RkafVersionOffset));
    }

    private static async Task WriteRkfwAsync(
        string outputPath,
        ReadOnlyMemory<byte> header,
        string bootPath,
        long bootSize,
        string updatePath,
        long updateSize,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        using var md5 = IncrementalHash.CreateHash(
            HashAlgorithmName.MD5);
        await using var output = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        progress.AddTotal(
            header.Length +
            bootSize +
            updateSize +
            DigestLength);
        await WriteHashedAsync(
                output,
                md5,
                header,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyHashedAsync(
                bootPath,
                bootSize,
                output,
                md5,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await CopyHashedAsync(
                updatePath,
                updateSize,
                output,
                md5,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        var digest = Encoding.ASCII.GetBytes(
            Convert.ToHexStringLower(md5.GetHashAndReset()));
        await output.WriteAsync(
                digest,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Advance(digest.Length);
    }

    private static async Task CopyHashedAsync(
        string path,
        long length,
        Stream output,
        IncrementalHash hash,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        await using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[CopyBufferSize];
        var remaining = length;
        while (remaining > 0) {
            var requested = (int)Math.Min(buffer.Length, remaining);
            var read = await input.ReadAsync(
                    buffer.AsMemory(0, requested),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new IOException(
                    $"'{path}' changed size while packing.");
            }

            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken)
                .ConfigureAwait(false);
            progress.Advance(read);
            remaining -= read;
        }

        if (input.ReadByte() != -1) {
            throw new IOException(
                $"'{path}' changed size while packing.");
        }
    }

    private static async Task WriteHashedAsync(
        Stream output,
        IncrementalHash hash,
        ReadOnlyMemory<byte> data,
        RockchipImageProgressReporter progress,
        CancellationToken cancellationToken
    ) {
        hash.AppendData(data.Span);
        await output.WriteAsync(data, cancellationToken)
            .ConfigureAwait(false);
        progress.Advance(data.Length);
    }
}
