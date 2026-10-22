namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal sealed class OperationLock : IDisposable {
    private static int _processHeld;
    private readonly FileStream _stream;
    private bool _disposed;

    private OperationLock(FileStream stream) {
        _stream = stream;
    }

    public static OperationLock Acquire() {
        var ownerPath = GetLockPath();
        if (Interlocked.CompareExchange(
                ref _processHeld,
                1,
                0) != 0) {
            throw Busy(ownerPath);
        }

        FileStream? stream = null;
        try {
            stream = new FileStream(
                ownerPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
#pragma warning disable CA1416 // Only Windows and Linux builds are supported.
            stream.Lock(0, 1);
#pragma warning restore CA1416
            return new OperationLock(stream);
        } catch (IOException error) {
            stream?.Dispose();
            Volatile.Write(ref _processHeld, 0);
            throw new InvalidOperationException(
                $"Device operation is busy. Another process holds " +
                $"the local hardware lock '{ownerPath}'.",
                error);
        } catch (UnauthorizedAccessException error) {
            stream?.Dispose();
            Volatile.Write(ref _processHeld, 0);
            throw new InvalidOperationException(
                $"Cannot access the local hardware lock '{ownerPath}'. " +
                "Check the ownership and permissions of its directory.",
                error);
        } catch {
            stream?.Dispose();
            Volatile.Write(ref _processHeld, 0);
            throw;
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        try {
#pragma warning disable CA1416 // Only Windows and Linux builds are supported.
            _stream.Unlock(0, 1);
#pragma warning restore CA1416
        } catch (IOException) {
        } finally {
            _stream.Dispose();
            Volatile.Write(ref _processHeld, 0);
        }
    }

    private static string GetLockPath() {
        var runtimeDirectory = Environment.GetEnvironmentVariable(
            "XDG_RUNTIME_DIR");
        var directory = default(string);
        if (!string.IsNullOrWhiteSpace(runtimeDirectory)) {
            directory = Path.Combine(
                runtimeDirectory,
                "ampheck-rockchip-toolkit");
        } else {
            var localData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            directory = string.IsNullOrWhiteSpace(localData)
                ? Path.Combine(
                    Path.GetTempPath(),
                    $"ampheck-rockchip-toolkit-{Environment.UserName}")
                : Path.Combine(
                    localData,
                    "Ampheck",
                    "RockchipToolkit");
        }

        Directory.CreateDirectory(directory);
        return Path.Combine(
            directory,
            "device-operation.lock");
    }

    private static InvalidOperationException Busy(
        string ownerPath) {
        return new InvalidOperationException(
            $"Device operation is busy in this process. " +
            $"The local hardware lock is '{ownerPath}'.");
    }
}
