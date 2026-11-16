namespace Ampheck.RockchipToolkit.Core.Ports.Files;

public interface IRemoteFileClient {
    ValueTask<IRemoteFile> OpenAsync(
        Uri uri,
        CancellationToken cancellationToken);
}
