using Ampheck.RockchipToolkit.Core.Ports.Files;

namespace Ampheck.RockchipToolkit.Runtime.Remote;

internal sealed class RemoteFile(
    HttpResponseMessage response,
    Stream content,
    long? length
) : IRemoteFile {
    public Stream Content { get; } = content;
    public long? Length { get; } = length;

    public async ValueTask DisposeAsync() {
        await Content.DisposeAsync().ConfigureAwait(false);
        response.Dispose();
    }
}
