using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;

internal sealed partial class SerialHub : IAsyncDisposable {
    private const int SubscriberCapacity = 1024;
    private readonly object _sync = new();
    private readonly HashSet<SerialSession> _sessions = [];
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _pump;
    private readonly ILogger _logger;
    private TaskCompletionSource<bool> _connectionReady =
        NewConnectionSignal();
    private ISerialPortConnection? _port;
    private volatile bool _connected;
    private bool _openFailureReported;
    private volatile string? _lastOpenError;
    private int _disposed;

    public SerialHub(
        SerialPortOptions options,
        ILogger logger
    ) {
        Options = options;
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
        var channel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
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
        }

        return ValueTask.FromResult(session);
    }

    public async ValueTask WaitForConnectionAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) {
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
        CancellationToken cancellationToken
    ) {
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

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        Exception? failure = null;
        try {
            await _pump.ConfigureAwait(false);
        } catch (OperationCanceledException) {
        } catch (Exception error) {
            failure = error;
        }

        lock (_sync) {
            foreach (var session in _sessions) {
                session.Complete();
            }
            _sessions.Clear();
        }

        try {
            _port?.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _writeGate.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _stop.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
