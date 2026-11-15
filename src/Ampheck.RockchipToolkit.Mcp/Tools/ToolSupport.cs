using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

internal static class ToolSupport {
    public static DeviceTarget CreateTarget(
        string transport,
        ushort? vendorId,
        ushort? productId) {
        return new DeviceTarget {
            Transport = transport.ToLowerInvariant() switch {
                "auto" => ToolkitTransport.Auto,
                "rockusb" => ToolkitTransport.RockUsb,
                "libusb" => ToolkitTransport.LibUsb,
                _ => throw new ArgumentException(
                    "transport must be auto, rockusb, or libusb.",
                    nameof(transport))
            },
            VendorId = vendorId,
            ProductId = productId
        };
    }

    public static void RequireOne(
        string? path,
        string? url,
        string description) {
        if (string.IsNullOrWhiteSpace(path) ==
            string.IsNullOrWhiteSpace(url)) {
            throw new ArgumentException(
                $"{description} requires exactly one of path or URL.");
        }
    }

    public static IProgress<long> CreateProgress(
        IProgress<ProgressNotificationValue>? progress,
        string label,
        long total) {
        var reporter = new ToolProgress(progress);
        return new CallbackProgress(value =>
            reporter.Report(label, value, total));
    }

    public static async ValueTask<InputFileReference> PrepareInputAsync(
        ToolkitRpcClient client,
        string path,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken,
        bool allowDirectory = false) {
        if (Directory.Exists(path)) {
            if (client.UseServerPaths && allowDirectory) {
                return new InputFileReference(
                    BlobId: null,
                    ServerPath: Path.GetFullPath(path));
            }

            throw new ArgumentException(
                "Directories must be provided as ZIP files.",
                nameof(path));
        }

        if (!File.Exists(path)) {
            throw new FileNotFoundException(
                $"Input file '{path}' was not found.",
                path);
        }

        if (client.UseServerPaths) {
            return new InputFileReference(
                BlobId: null,
                ServerPath: Path.GetFullPath(path));
        }

        var blob = await client.UploadBlobAsync(
                path,
                CreateProgress(
                    progress,
                    Path.GetFileName(path),
                    new FileInfo(path).Length),
                cancellationToken);
        return new InputFileReference(
            blob.BlobId,
            ServerPath: null);
    }

    public static async ValueTask<FileReference> SaveOutputAsync(
        ToolkitRpcClient client,
        FileReference file,
        string destination,
        IProgress<ProgressNotificationValue>? progress,
        CancellationToken cancellationToken) {
        if (file.Path is not null) {
            return file;
        }

        var blob = new BlobReference {
            BlobId = file.BlobId ??
                throw new InvalidDataException(
                    "The server returned no blob or local path."),
            FileName = file.FileName,
            Length = file.Length,
            Sha256 = file.Sha256
        };
        var path = Path.GetFullPath(destination);
        if (Directory.Exists(path)) {
            path = Path.Combine(path, blob.FileName);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        await client.DownloadBlobAsync(
            blob,
            path,
            CreateProgress(progress, blob.FileName, blob.Length),
            cancellationToken);
        return new FileReference {
            Path = path,
            FileName = blob.FileName,
            Length = blob.Length,
            Sha256 = blob.Sha256
        };
    }
}

internal sealed record InputFileReference(
    string? BlobId,
    string? ServerPath);

internal sealed class ToolProgress(
    IProgress<ProgressNotificationValue>? progress) {
    private long _sequence;

    public void Report(TaskEventNotification value) {
        var detail = value.Current is { } current &&
            value.Total is { } total
                ? $"{value.Message} ({current:N0}/{total:N0})"
                : value.Message;
        Report(detail);
    }

    public void Report(
        string label,
        long current,
        long total) {
        Report($"{label} ({current:N0}/{total:N0})");
    }

    public void Report(string message) {
        if (progress is null) {
            return;
        }

        progress.Report(new ProgressNotificationValue {
            Progress = Interlocked.Increment(ref _sequence),
            Message = message
        });
    }
}
