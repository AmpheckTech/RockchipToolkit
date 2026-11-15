using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Server.Rpc;

internal static class RpcFileAccess {
    public static string RequireServerPath(
        JsonRpcCallContext context,
        string? path,
        string description) {
        if (!context.IsLocalClient) {
            throw new ToolkitRpcException(
                JsonRpcErrorCodes.InvalidParams,
                $"{description} is only available to loopback clients.");
        }

        if (string.IsNullOrWhiteSpace(path)) {
            throw new InvalidDataException(
                $"{description} path is empty.");
        }

        return Path.GetFullPath(path);
    }

    public static FileReference CreateFileReference(
        string path) {
        var file = new FileInfo(path);
        if (!file.Exists) {
            throw new FileNotFoundException(
                $"Output file '{path}' was not created.",
                path);
        }

        return new FileReference {
            Path = file.FullName,
            FileName = file.Name,
            Length = file.Length
        };
    }

    public static FileReference CreateFileReference(
        BlobReference blob) {
        return new FileReference {
            BlobId = blob.BlobId,
            FileName = blob.FileName,
            Length = blob.Length,
            Sha256 = blob.Sha256
        };
    }
}
