namespace Ampheck.RockchipToolkit.Hardware.Serial;

internal sealed partial class SerialHub {
    private async Task RunAsync() {
        while (!_stop.IsCancellationRequested) {
            if (!_connected) {
                try {
                    Open();
                } catch (Exception error) when (
                    error is IOException or UnauthorizedAccessException or
                    InvalidOperationException or ArgumentException) {
                    if (!_openFailureReported) {
                        _lastOpenError = error.Message;
                        _openFailureReported = true;
                    }

                    await DelayAsync().ConfigureAwait(false);
                    continue;
                }
            }

            try {
                var buffer = new byte[4096];
                int read;
                try {
                    read = _port!.Read(
                        buffer,
                        0,
                        buffer.Length);
                } catch (TimeoutException) {
                    continue;
                }

                if (read == 0) {
                    continue;
                }

                var data = buffer.AsSpan(0, read).ToArray();
                Publish(data);
            } catch (OperationCanceledException) when (
                _stop.IsCancellationRequested) {
                return;
            } catch (Exception error) when (
                error is IOException or InvalidOperationException or
                UnauthorizedAccessException) {
                if (_stop.IsCancellationRequested) {
                    return;
                }

                MarkDisconnected();
                await DelayAsync().ConfigureAwait(false);
            }
        }
    }

    private void Open() {
        var port = SerialPortConnectionFactory.Create(
            Options);
        try {
            port.Open();
            Volatile.Write(ref _port, port);
            _connected = true;
            _openFailureReported = false;
            _lastOpenError = null;
            _connectionReady.TrySetResult(true);
        } catch {
            port.Dispose();
            throw;
        }
    }

    private void MarkDisconnected() {
        if (!_connected) {
            return;
        }

        _connected = false;
        var nextConnection = NewConnectionSignal();
        var previousConnection = _connectionReady;
        _connectionReady = nextConnection;
        previousConnection.TrySetResult(false);
        var port = Interlocked.Exchange(ref _port, null);
        port?.Dispose();
    }

    private void Publish(ReadOnlyMemory<byte> value) {
        List<SerialSession> sessions;
        lock (_sync) {
            sessions = [.. _sessions];
        }

        foreach (var session in sessions) {
            session.TryPublish(value);
        }
    }

    private Task DelayAsync() =>
        Task.Delay(TimeSpan.FromSeconds(1), _stop.Token);

    private static TaskCompletionSource<bool> NewConnectionSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
