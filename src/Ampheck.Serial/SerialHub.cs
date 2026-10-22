using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Ampheck.Serial;

internal sealed class SerialHub : IAsyncDisposable {
    private const int SubscriberCapacity = 1024;
    private readonly object _sync = new();
    private readonly HashSet<SerialSession> _sessions = [];
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _pump;
    private readonly ILogger _logger;
    private readonly byte[] _history;
    private TaskCompletionSource<bool> _connectionReady =
        NewConnectionSignal();
    private ISerialPortConnection? _port;
    private int _historyStart;
    private int _historyLength;
    private volatile bool _connected;
    private bool _openFailureReported;
    private volatile string? _lastOpenError;
    private int _disposed;

    public SerialHub(
        SerialPortOptions options,
        int historyBytes,
        ILogger logger) {
        Options = options;
        _history = new byte[historyBytes];
        _logger = logger;
        _pump = Task.Factory.StartNew(
                RunAsync,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();
    }

    public SerialPortOptions Options { get; }

    public ValueTask<SerialSession> OpenSessionAsync() {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        var channel = Channel.CreateBounded<SerialEvent>(
            new BoundedChannelOptions(SubscriberCapacity) {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        var session = new SerialSession(
            this,
            Options.PortName,
            channel);
        lock (_sync) {
            _sessions.Add(session);
            var history = GetHistorySnapshot(_history.Length);
            if (history.Length > 0) {
                session.TryPublish(new SerialEvent(
                    SerialEventKind.Data,
                    history));
            }
        }

        return ValueTask.FromResult(session);
    }

    public async ValueTask WaitForConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        if (timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var ready = Volatile.Read(ref _connectionReady);
        bool connected;
        try {
            connected = await ready.Task.WaitAsync(
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
        } catch (TimeoutException error) when (
            _lastOpenError is not null) {
            throw new IOException(
                $"Unable to open serial port '{Options.PortName}': " +
                _lastOpenError,
                error);
        }

        if (!connected) {
            throw new IOException(
                $"Serial port '{Options.PortName}' disconnected." +
                (_lastOpenError is null
                    ? string.Empty
                    : $" {_lastOpenError}"));
        }
    }

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) {
        await _writeGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await Volatile.Read(ref _connectionReady).Task.WaitAsync(
                    TimeSpan.FromSeconds(10),
                    cancellationToken)
                .ConfigureAwait(false);
            var port = Volatile.Read(ref _port);
            if (port is null || !_connected) {
                throw new IOException(
                    $"Serial port '{Options.PortName}' is disconnected.");
            }

            port.Write(data);
        } catch (Exception error) when (
            error is IOException or InvalidOperationException) {
            MarkDisconnected(error);
            throw;
        } finally {
            _writeGate.Release();
        }
    }

    public ValueTask DetachAsync(SerialSession session) {
        lock (_sync) {
            _sessions.Remove(session);
        }

        return ValueTask.CompletedTask;
    }

    public ReadOnlyMemory<byte> GetHistorySnapshot(int maximumBytes) {
        lock (_sync) {
            var history = GetHistory();
            return history.Length <= maximumBytes
                ? history
                : history[^maximumBytes..];
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        try {
            await _pump.ConfigureAwait(false);
        } catch (OperationCanceledException) {
        }

        lock (_sync) {
            foreach (var session in _sessions) {
                session.TryPublish(new SerialEvent(
                    SerialEventKind.Disconnected,
                    ReadOnlyMemory<byte>.Empty,
                    "Serial service stopped."));
                session.Complete();
            }
            _sessions.Clear();
        }

        _port?.Dispose();
        _writeGate.Dispose();
        _stop.Dispose();
    }

    private async Task RunAsync() {
        while (!_stop.IsCancellationRequested) {
            if (!_connected) {
                try {
                    Open();
                    PublishStatus(
                        SerialEventKind.Connected,
                        $"Connected to {Options.PortName} at " +
                        $"{Options.BaudRate} baud.");
                } catch (Exception error) when (
                    error is IOException or UnauthorizedAccessException or
                    InvalidOperationException or ArgumentException) {
                    if (!_openFailureReported) {
                        _lastOpenError = error.Message;
                        _openFailureReported = true;
                        PublishStatus(
                            SerialEventKind.Disconnected,
                            $"Unable to open {Options.PortName}; retrying: " +
                            error.Message);
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
                lock (_sync) {
                    AppendHistory(data);
                }

                Publish(new SerialEvent(
                    SerialEventKind.Data,
                    data));
            } catch (OperationCanceledException) when (
                _stop.IsCancellationRequested) {
                return;
            } catch (Exception error) when (
                error is IOException or InvalidOperationException or
                UnauthorizedAccessException) {
                if (_stop.IsCancellationRequested) {
                    return;
                }

                MarkDisconnected(error);
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
            _logger.LogInformation(
                "Serial port {PortName} opened at {BaudRate} baud",
                Options.PortName,
                Options.BaudRate);
        } catch {
            port.Dispose();
            throw;
        }
    }

    private void MarkDisconnected(Exception error) {
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
        _logger.LogWarning(
            error,
            "Serial port {PortName} disconnected",
            Options.PortName);
        PublishStatus(
            SerialEventKind.Disconnected,
            $"Serial port {Options.PortName} disconnected; waiting to reconnect.");
    }

    private void PublishStatus(
        SerialEventKind kind,
        string message) {
        Publish(new SerialEvent(
            kind,
            ReadOnlyMemory<byte>.Empty,
            message));
    }

    private void Publish(SerialEvent value) {
        List<SerialSession> sessions;
        lock (_sync) {
            sessions = [.. _sessions];
        }

        foreach (var session in sessions) {
            session.TryPublish(value);
        }
    }

    private ReadOnlyMemory<byte> GetHistory() {
        if (_historyLength == 0) {
            return ReadOnlyMemory<byte>.Empty;
        }

        var result = new byte[_historyLength];
        var first = Math.Min(
            _historyLength,
            _history.Length - _historyStart);
        Array.Copy(
            _history,
            _historyStart,
            result,
            0,
            first);
        if (first < _historyLength) {
            Array.Copy(
                _history,
                0,
                result,
                first,
                _historyLength - first);
        }

        return result;
    }

    private void AppendHistory(ReadOnlySpan<byte> data) {
        if (data.Length >= _history.Length) {
            data[^_history.Length..].CopyTo(_history);
            _historyStart = 0;
            _historyLength = _history.Length;
            return;
        }

        var overflow = _historyLength + data.Length - _history.Length;
        if (overflow > 0) {
            _historyStart =
                (_historyStart + overflow) % _history.Length;
            _historyLength -= overflow;
        }

        var write = (_historyStart + _historyLength) %
            _history.Length;
        var first = Math.Min(
            data.Length,
            _history.Length - write);
        data[..first].CopyTo(_history.AsSpan(write));
        if (first < data.Length) {
            data[first..].CopyTo(_history);
        }

        _historyLength += data.Length;
    }

    private Task DelayAsync() =>
        Task.Delay(TimeSpan.FromSeconds(1), _stop.Token);

    private static TaskCompletionSource<bool> NewConnectionSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
