namespace Ampheck.RockchipToolkit.Core.Ports.Files;

public interface IRemoteFile : IAsyncDisposable {
    Stream Content { get; }
    long? Length { get; }
}
