using Ampheck.RockchipToolkit.Adb.Client.Connection;
using Ampheck.RockchipToolkit.Core.Ports.Adb;

namespace Ampheck.RockchipToolkit.Adb.Client;

public sealed class AdbShell : IAdbShellStream, IAdbShellTerminal {
    private readonly IAdbShellStream _shell;
    private bool _disposed;

    internal AdbShell(IAdbShellStream shell) {
        _shell = shell;
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.WriteAsync(data, cancellationToken);
    }

    public Task ReadAsync(
        Action<string> output,
        CancellationToken cancellationToken = default
    ) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _shell.ReadAsync(
            output,
            cancellationToken);
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await _shell.DisposeAsync().ConfigureAwait(false);
    }
}
