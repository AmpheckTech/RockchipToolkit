namespace Ampheck.RockchipToolkit.Server.Devices;

internal sealed class DeviceOperationGate : IDisposable {
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<IDisposable> AcquireAsync(
        CancellationToken cancellationToken) {
        await _gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return new Lease(_gate);
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Lease(SemaphoreSlim gate) : IDisposable {
        private int _disposed;

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) {
                gate.Release();
            }
        }
    }
}
