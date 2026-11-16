using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Adb.Server;

/// <summary>
/// Starts the official Android Debug Bridge server on demand.
/// </summary>
internal sealed class AdbServerProcess : IAsyncDisposable {
    private static readonly TimeSpan StartupTimeout =
        TimeSpan.FromSeconds(5);
    private readonly AdbServerOptions _connectOptions;
    private readonly bool _listenOnAllInterfaces;
    private readonly ILogger<AdbServerProcess> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private bool _disposed;

    public AdbServerProcess(
        string toolkitHost,
        int port,
        ILoggerFactory loggerFactory
    ) {
        _connectOptions = new AdbServerOptions {
            Host = "127.0.0.1",
            Port = port
        };
        _listenOnAllInterfaces = !IsLoopbackHost(toolkitHost);
        _logger = loggerFactory.CreateLogger<AdbServerProcess>();
    }

    public async Task EnsureRunningAsync(
        CancellationToken cancellationToken = default
    ) {
        if (await AdbServerClient.IsAvailableAsync(
                cancellationToken,
                _connectOptions)
            .ConfigureAwait(false)) {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (await AdbServerClient.IsAvailableAsync(
                    cancellationToken,
                    _connectOptions)
                .ConfigureAwait(false)) {
                return;
            }

            if (_process is { HasExited: false } process) {
                await WaitUntilAvailableAsync(
                        process,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            DisposeProcess();
            var startedProcess = Start();
            _process = startedProcess;
            try {
                await WaitUntilAvailableAsync(
                        startedProcess,
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch {
                DisposeProcess();
                throw;
            }
        } finally {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            DisposeProcess();
        } finally {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private Process Start() {
        var executable = ResolveExecutable();
        var startInfo = new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (_listenOnAllInterfaces) {
            startInfo.ArgumentList.Add("-a");
        }

        startInfo.ArgumentList.Add("-P");
        startInfo.ArgumentList.Add(
            _connectOptions.Port.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("nodaemon");
        startInfo.ArgumentList.Add("server");

        try {
            var process = Process.Start(startInfo) ??
                throw new InvalidOperationException(
                    "The official adb server process did not start.");
            process.OutputDataReceived += (_, args) => {
                if (args.Data is not null) {
                    _logger.LogDebug("adb: {Line}", args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) => {
                if (args.Data is not null) {
                    _logger.LogDebug("adb: {Line}", args.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _logger.LogInformation(
                "Started official ADB server using {Executable} " +
                "on port {Port} (listenOnAllInterfaces: {AllInterfaces})",
                executable,
                _connectOptions.Port,
                _listenOnAllInterfaces);
            return process;
        } catch (Win32Exception error) when (
            error.NativeErrorCode is 2 or 3) {
            throw new InvalidOperationException(
                $"The official ADB executable '{executable}' was not found. " +
                "Install Android SDK Platform Tools, add adb to PATH, " +
                "or set ARKT_ADB to the adb executable path.",
                error);
        } catch (Win32Exception error) {
            throw new InvalidOperationException(
                $"Unable to start the official ADB server using " +
                $"'{executable}': {error.Message}",
                error);
        }
    }

    private async Task WaitUntilAvailableAsync(
        Process process,
        CancellationToken cancellationToken
    ) {
        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline) {
            cancellationToken.ThrowIfCancellationRequested();
            if (await AdbServerClient.IsAvailableAsync(
                    cancellationToken,
                    _connectOptions)
                .ConfigureAwait(false)) {
                return;
            }

            if (process.HasExited) {
                throw new InvalidOperationException(
                    "The official adb server exited with code " +
                    $"{process.ExitCode} before accepting connections.");
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(100),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"The official adb server did not become available on " +
            $"127.0.0.1:{_connectOptions.Port} within " +
            $"{StartupTimeout.TotalSeconds:F0} seconds.");
    }

    private void DisposeProcess() {
        var process = _process;
        _process = null;
        if (process is null) {
            return;
        }

        try {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        } catch (Exception error) when (
            error is InvalidOperationException or Win32Exception) {
            _logger.LogDebug(
                error,
                "Unable to stop the official adb server process");
        } finally {
            process.Dispose();
        }
    }

    private static string ResolveExecutable() {
        var configured = Environment.GetEnvironmentVariable(
            "ARKT_ADB");
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }

        var fileName = OperatingSystem.IsWindows()
            ? "adb.exe"
            : "adb";
        var local = Path.Combine(
            AppContext.BaseDirectory,
            "platform-tools",
            fileName);
        if (File.Exists(local)) {
            return local;
        }

        foreach (var variable in new[] {
            "ANDROID_SDK_ROOT",
            "ANDROID_HOME"
        }) {
            var sdkRoot = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(sdkRoot)) {
                continue;
            }

            var candidate = Path.Combine(
                sdkRoot,
                "platform-tools",
                fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        return fileName;
    }

    private static bool IsLoopbackHost(string host) {
        return host.Equals(
                "localhost",
                StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out var address) &&
            IPAddress.IsLoopback(address);
    }
}
