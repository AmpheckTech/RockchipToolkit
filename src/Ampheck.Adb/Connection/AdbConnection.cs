using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text;
using Ampheck.Adb.Authentication;
using Ampheck.Adb.Protocol;
using Ampheck.Adb.Usb;

namespace Ampheck.Adb.Connection;

/// <summary>Owns one ADB USB session and speaks the host-side ADB protocol.</summary>
public sealed class AdbConnection : IAsyncDisposable {
    private readonly IAdbTransport _transport;
    private readonly uint _maximumPayload;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<uint, AdbStream> _streams = [];
    private readonly CancellationTokenSource _readStop = new();
    private uint _nextLocalId;
    private Task? _readLoop;
    private bool _connected;
    private bool _disposed;

    public AdbConnection(
        IAdbTransport transport,
        uint maximumPayload = 256 * 1024) {
        _transport = transport ??
            throw new ArgumentNullException(nameof(transport));
        if (maximumPayload is 0 or > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayload),
                "The ADB maximum payload must fit in a byte array.");
        }

        _maximumPayload = maximumPayload;
    }

    public string? Banner { get; private set; }
    public bool IsOnline =>
        _connected &&
        _readLoop is { IsCompleted: false };

    public async ValueTask<AdbConnectionState> ConnectAsync(
        bool waitForAuthorization,
        Action<string>? status,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connected) {
            return AdbConnectionState.Online;
        }

        await AdbProtocol.SendAsync(
                _transport,
                AdbCommand.Connect,
                AdbProtocol.VersionValue,
                _maximumPayload,
                AdbProtocol.ConnectPayload(),
                cancellationToken)
            .ConfigureAwait(false);

        AdbKey? key = null;
        var signed = false;
        var sentPublicKey = false;
        try {
            while (true) {
                var message = await AdbProtocol.ReadAsync(
                        _transport,
                        _maximumPayload,
                        cancellationToken)
                    .ConfigureAwait(false);
                switch (message.Command) {
                    case AdbCommand.Connect:
                        Banner = DecodeBanner(message.Data);
                        _connected = true;
                        _readLoop = Task.Run(ReadLoopAsync);
                        return AdbConnectionState.Online;
                    case AdbCommand.Auth when message.Arg0 == 1:
                        key ??= AdbKeyStore.Load(
                            createIfMissing: waitForAuthorization);
                        if (key is null) {
                            return AdbConnectionState.Unauthorized;
                        }

                        if (!signed) {
                            status?.Invoke(
                                "Signing the ADB authentication token");
                            await AdbProtocol.SendAsync(
                                    _transport,
                                    AdbCommand.Auth,
                                    2,
                                    0,
                                    key.Sign(message.Data),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            signed = true;
                            continue;
                        }

                        if (!sentPublicKey) {
                            status?.Invoke(
                                "Approve USB debugging on the device");
                            await AdbProtocol.SendAsync(
                                    _transport,
                                    AdbCommand.Auth,
                                    3,
                                    0,
                                    key.PublicKeyPayload(),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            sentPublicKey = true;
                            if (!waitForAuthorization) {
                                return AdbConnectionState.Unauthorized;
                            }

                            continue;
                        }

                        if (!waitForAuthorization) {
                            return AdbConnectionState.Unauthorized;
                        }

                        await Task.Delay(
                                TimeSpan.FromMilliseconds(250),
                                cancellationToken)
                            .ConfigureAwait(false);
                        break;
                }
            }
        } finally {
            key?.Dispose();
        }
    }

    public async ValueTask RunShellAsync(
        string command,
        Action<string> output,
        CancellationToken cancellationToken = default) {
        await using var shell = await OpenShellAsync(
                command,
                cancellationToken)
            .ConfigureAwait(false);
        await shell.ReadAsync(output, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<IAdbShellStream> OpenShellAsync(
        string? command,
        CancellationToken cancellationToken = default) {
        if (command is not null) {
            var commandStream = await OpenServiceAsync(
                    $"shell:{command}",
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbShellSession(commandStream);
        }

        try {
            var terminalStream = await OpenServiceAsync(
                    "shell,v2,TERM=xterm-256color,pty:",
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbShellProtocolSession(
                terminalStream,
                Timeout.InfiniteTimeSpan);
        } catch (IOException) {
            var legacyStream = await OpenServiceAsync(
                    "shell:",
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbShellSession(legacyStream);
        }
    }

    public async ValueTask<AdbStream> OpenServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_connected) {
            throw new InvalidOperationException(
                "The ADB connection is not online.");
        }

        var localId = Interlocked.Increment(ref _nextLocalId);
        var service = Encoding.UTF8.GetBytes(
            serviceName + "\0");
        var stream = new AdbStream(
            _maximumPayload,
            localId,
            SendStreamCommandAsync,
            RemoveStream);
        _streams[localId] = stream;
        try {
            await SendStreamCommandAsync(
                localId,
                0,
                AdbCommand.Open,
                service,
                cancellationToken)
            .ConfigureAwait(false);

            while (true) {
                var message = await stream.ReadOpeningMessageAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
                if (message.Command == AdbCommand.Okay &&
                    message.Arg1 == localId) {
                    stream.Open(message.Arg0);
                    return stream;
                }

                if (message.Command == AdbCommand.Write &&
                    message.Arg1 == localId) {
                    await stream.AcceptOpeningMessageAsync(
                            message,
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                if (message.Command == AdbCommand.Close &&
                    message.Arg1 == localId) {
                    throw new IOException(
                        $"The ADB device rejected service " +
                        $"'{serviceName}'.");
                }

                throw new IOException(
                    $"Unexpected ADB response " +
                    $"{message.Command} for service " +
                    $"'{serviceName}'.");
            }
        } catch {
            await stream.AbortAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _readStop.Cancel();
        Exception? failure = null;
        foreach (var stream in _streams.Values) {
            try {
                await stream.AbortAsync(
                        new ObjectDisposedException(
                            nameof(AdbConnection)))
                    .ConfigureAwait(false);
            } catch (Exception error) {
                failure ??= error;
            }
        }

        try {
            await _transport.DisposeAsync().ConfigureAwait(false);
        } catch (Exception error) {
            failure ??= error;
        }

        if (_readLoop is not null) {
            try {
                await _readLoop.ConfigureAwait(false);
            } catch {
            }
        }

        try {
            _readStop.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _sendGate.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task ReadLoopAsync() {
        Exception? failure = null;
        try {
            while (!_readStop.IsCancellationRequested) {
                var message = await AdbProtocol.ReadAsync(
                        _transport,
                        _maximumPayload,
                        _readStop.Token,
                        Timeout.InfiniteTimeSpan)
                    .ConfigureAwait(false);
                if (_streams.TryGetValue(
                    message.Arg1,
                    out var stream)) {
                    stream.Receive(message);
                }
            }
        } catch (Exception error) when (
            error is IOException or
            OperationCanceledException or
            ObjectDisposedException or
            System.ComponentModel.Win32Exception) {
            failure = error;
        } finally {
            foreach (var stream in _streams.Values) {
                await stream.AbortAsync(failure)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask SendStreamCommandAsync(
        uint localId,
        uint remoteId,
        AdbCommand command,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) {
        await _sendGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await AdbProtocol.SendAsync(
                    _transport,
                    command,
                    localId,
                    remoteId,
                    data,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            _sendGate.Release();
        }
    }

    private void RemoveStream(uint localId) {
        _streams.TryRemove(localId, out _);
    }

    private static string DecodeBanner(ReadOnlySpan<byte> data) {
        return Encoding.UTF8.GetString(data)
            .TrimEnd('\0');
    }
}
