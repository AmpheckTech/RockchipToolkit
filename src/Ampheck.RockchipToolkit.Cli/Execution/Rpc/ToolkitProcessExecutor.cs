using System.Diagnostics;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class ToolkitProcessExecutor : IToolkitExecutor {
    private readonly ToolkitTransferLimits _limits;

    public ToolkitProcessExecutor(
        ToolkitTransferLimits? limits = null) {
        _limits = limits ?? ToolkitTransferLimits.Default;
        _limits.Validate();
    }

    public async Task<ToolkitResult> ExecuteAsync(ToolkitRequest request, Func<ToolkitEvent, ValueTask> report, CancellationToken cancellationToken) {
        var startInfo = CreateStartInfo(request);
        startInfo.Environment["ARKT_DIRECT_WORKER"] = "1";
        startInfo.Environment["ARKT_MAX_ATTACHMENT_BYTES"] =
            _limits.MaximumAttachmentBytes.ToString();
        startInfo.Environment["ARKT_MAX_REQUEST_BYTES"] =
            _limits.MaximumRequestBytes.ToString();
        startInfo.Environment["ARKT_MAX_EXPANDED_BYTES"] =
            _limits.MaximumExpandedBytes.ToString();
        startInfo.Environment["ARKT_MAX_ATTACHMENTS"] =
            _limits.MaximumAttachments.ToString();
        startInfo.Environment["ARKT_MAX_ARCHIVE_ENTRIES"] =
            _limits.MaximumArchiveEntries.ToString();
        startInfo.Environment["ARKT_MAX_ARCHIVE_PATH_LENGTH"] =
            _limits.MaximumArchivePathLength.ToString();
        using var process = new Process {
            StartInfo = startInfo
        };
        if (!process.Start()) {
            throw new InvalidOperationException(
                "Unable to start the toolkit operation process.");
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string? lastError = null;
        var outputTask = PumpAsync(
            process.StandardOutput,
            "stdout",
            report,
            stop,
            onErrorLine: null);
        var errorTask = PumpAsync(
            process.StandardError,
            "stderr",
            report,
            stop,
            line => Volatile.Write(ref lastError, line));
        try {
            await process.WaitForExitAsync(stop.Token).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            return new ToolkitResult(
                process.ExitCode == 0,
                process.ExitCode,
                process.ExitCode == 0
                    ? null
                    : Volatile.Read(ref lastError) ??
                        $"Toolkit worker exited with code " +
                        $"{process.ExitCode}.");
        } catch (OperationCanceledException) {
            TryKill(process);
            throw;
        } finally {
            if (!process.HasExited) {
                TryKill(process);
            }
        }
    }

    private static ProcessStartInfo CreateStartInfo(ToolkitRequest request) {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The toolkit executable path is unavailable.");
        var frameworkDependent = Path
            .GetFileNameWithoutExtension(executable)
            .Equals(
                "dotnet",
                StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (frameworkDependent) {
            startInfo.ArgumentList.Add(
                Environment.GetCommandLineArgs().FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "The toolkit assembly path is unavailable."));
        }

        AddOperationArguments(startInfo.ArgumentList, request);
        return startInfo;
    }

    private static void AddOperationArguments(IList<string> arguments, ToolkitRequest request) {
        AddOperation(arguments, request.Operation);
        AddOptional(arguments, "--transport", FormatTransport(request.Transport));
        AddOptional(arguments, "--vid", request.VendorId?.ToString());
        AddOptional(arguments, "--pid", request.ProductId?.ToString());
        AddOptional(arguments, "--loader", request.Loader);
        if (request.Verbose) {
            arguments.Add("--verbose");
        }

        switch (request.Operation) {
            case ToolkitOperation.Loader:
                AddRequired(arguments, request.File, "loader file");
                break;
            case ToolkitOperation.Flash:
                AddRequired(arguments, request.Source, "firmware source");
                if (!request.ResetAfterFlash) {
                    arguments.Add("--no-reset");
                }

                break;
            case ToolkitOperation.Read:
                AddRequired(arguments, request.File, "output file");
                AddOptional(arguments, "--sector", request.Sector?.ToString());
                AddOptional(arguments, "--count", request.Count?.ToString());
                break;
            case ToolkitOperation.Write:
                AddRequired(arguments, request.File, "input file");
                AddOptional(arguments, "--sector", request.Sector?.ToString());
                AddOptional(arguments, "--backup", request.Backup);
                AddOptional(arguments, "--length", request.WriteLength?.ToString());
                AddOptional(arguments, "--source-offset", request.WriteSourceOffset?.ToString());
                if (!request.EraseBeforeWrite) {
                    arguments.Add("--no-erase");
                }

                break;
            case ToolkitOperation.Erase:
                AddOptional(arguments, "--sector", request.Sector?.ToString());
                AddOptional(arguments, "--count", request.Count?.ToString());
                break;
            case ToolkitOperation.Storage:
                AddRequired(
                    arguments,
                    FormatStorage(request.Storage),
                    "storage type");
                break;
            case ToolkitOperation.Reset:
                if (FormatResetMode(request.ResetMode) is { } resetMode) {
                    arguments.Add(resetMode);
                }

                break;
        }
    }

    private static void AddOperation(IList<string> arguments, ToolkitOperation operation) {
        switch (operation) {
            case ToolkitOperation.ChipInfo:
                arguments.Add("chip");
                arguments.Add("info");
                break;
            case ToolkitOperation.ChipId:
                arguments.Add("chip");
                arguments.Add("id");
                break;
            default:
                arguments.Add(operation.ToString().ToLowerInvariant());
                break;
        }
    }

    private static string FormatTransport(ToolkitTransport transport) {
        return transport switch {
            ToolkitTransport.RockUsb => "rockusb",
            ToolkitTransport.LibUsb => "libusb",
            _ => "auto"
        };
    }

    private static string? FormatStorage(ToolkitStorage? storage) {
        return storage switch {
            ToolkitStorage.Emmc => "emmc",
            ToolkitStorage.SdCard => "sd-card",
            ToolkitStorage.SpiNor => "spi-nor",
            _ => null
        };
    }

    private static string? FormatResetMode(ToolkitResetMode? mode) {
        return mode switch {
            ToolkitResetMode.ResetMassStorage => "reset-mass-storage",
            ToolkitResetMode.PowerOff => "power-off",
            ToolkitResetMode.ResetMaskRom => "reset-mask-rom",
            ToolkitResetMode.Disconnect => "disconnect",
            _ => null
        };
    }

    private static void AddOptional(IList<string> arguments, string option, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        arguments.Add(option);
        arguments.Add(value);
    }

    private static void AddRequired(IList<string> arguments, string? value, string description) {
        arguments.Add(value ?? throw new ArgumentException(
            $"The toolkit request contains no {description}."));
    }

    private static async Task PumpAsync(
        StreamReader reader,
        string source,
        Func<ToolkitEvent, ValueTask> report,
        CancellationTokenSource stop,
        Action<string>? onErrorLine) {
        try {
            while (await reader.ReadLineAsync(stop.Token).ConfigureAwait(false) is { } line) {
                var value = source == "stdout" &&
                    ToolkitWorkerEvent.TryParse(line, out var progress)
                        ? progress
                        : new ToolkitEvent(source, line);
                await report(value).ConfigureAwait(false);
                if (source == "stderr") {
                    onErrorLine?.Invoke(line);
                }
            }
        } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
        } catch {
            await stop.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void TryKill(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (Exception error) when (
            error is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException) {
        }
    }
}
