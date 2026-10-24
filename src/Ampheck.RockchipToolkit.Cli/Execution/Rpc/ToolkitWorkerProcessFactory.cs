using System.Diagnostics;
using System.Globalization;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitWorkerProcessFactory {
    public static ProcessStartInfo Create(
        ToolkitRequest request,
        ToolkitTransferLimits limits) {
        limits.Validate();
        var executable = Environment.ProcessPath ??
            throw new InvalidOperationException(
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
                Environment.GetCommandLineArgs().FirstOrDefault() ??
                throw new InvalidOperationException(
                    "The toolkit assembly path is unavailable."));
        }

        AddWorkerContext(startInfo.ArgumentList, limits);
        AddOperationArguments(startInfo.ArgumentList, request);
        return startInfo;
    }

    private static void AddWorkerContext(
        IList<string> arguments,
        ToolkitTransferLimits limits) {
        arguments.Add("--worker");
        AddValue(
            arguments,
            "--attachment-bytes",
            limits.MaximumAttachmentBytes);
        AddValue(
            arguments,
            "--request-bytes",
            limits.MaximumRequestBytes);
        AddValue(
            arguments,
            "--expanded-bytes",
            limits.MaximumExpandedBytes);
        AddValue(
            arguments,
            "--attachments",
            limits.MaximumAttachments);
        AddValue(
            arguments,
            "--archive-entries",
            limits.MaximumArchiveEntries);
        AddValue(
            arguments,
            "--archive-path-length",
            limits.MaximumArchivePathLength);
        arguments.Add("--");
    }

    private static void AddValue(
        IList<string> arguments,
        string option,
        long value) {
        arguments.Add(option);
        arguments.Add(value.ToString(
            CultureInfo.InvariantCulture));
    }

    private static void AddOperationArguments(
        IList<string> arguments,
        ToolkitRequest request) {
        AddOperation(arguments, request.Operation);
        AddOptional(
            arguments,
            "--transport",
            FormatTransport(request.Transport));
        AddOptional(
            arguments,
            "--vid",
            request.VendorId?.ToString(
                CultureInfo.InvariantCulture));
        AddOptional(
            arguments,
            "--pid",
            request.ProductId?.ToString(
                CultureInfo.InvariantCulture));
        AddOptional(arguments, "--loader", request.Loader);
        if (request.Verbose) {
            arguments.Add("--verbose");
        }

        switch (request.Operation) {
            case ToolkitOperation.Loader:
                AddRequired(
                    arguments,
                    request.File,
                    "loader file");
                break;
            case ToolkitOperation.Flash:
                AddRequired(
                    arguments,
                    request.Source,
                    "firmware source");
                if (!request.ResetAfterFlash) {
                    arguments.Add("--no-reset");
                }

                break;
            case ToolkitOperation.Read:
                AddRequired(
                    arguments,
                    request.File,
                    "output file");
                AddOptional(
                    arguments,
                    "--sector",
                    request.Sector?.ToString(
                        CultureInfo.InvariantCulture));
                AddOptional(
                    arguments,
                    "--count",
                    request.Count?.ToString(
                        CultureInfo.InvariantCulture));
                break;
            case ToolkitOperation.Write:
                AddRequired(
                    arguments,
                    request.File,
                    "input file");
                AddOptional(
                    arguments,
                    "--sector",
                    request.Sector?.ToString(
                        CultureInfo.InvariantCulture));
                AddOptional(
                    arguments,
                    "--backup",
                    request.Backup);
                AddOptional(
                    arguments,
                    "--length",
                    request.WriteLength?.ToString(
                        CultureInfo.InvariantCulture));
                AddOptional(
                    arguments,
                    "--source-offset",
                    request.WriteSourceOffset?.ToString(
                        CultureInfo.InvariantCulture));
                if (!request.EraseBeforeWrite) {
                    arguments.Add("--no-erase");
                }

                break;
            case ToolkitOperation.Erase:
                AddOptional(
                    arguments,
                    "--sector",
                    request.Sector?.ToString(
                        CultureInfo.InvariantCulture));
                AddOptional(
                    arguments,
                    "--count",
                    request.Count?.ToString(
                        CultureInfo.InvariantCulture));
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

    private static void AddOperation(
        IList<string> arguments,
        ToolkitOperation operation) {
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
                arguments.Add(
                    operation.ToString().ToLowerInvariant());
                break;
        }
    }

    private static string FormatTransport(
        ToolkitTransport transport) {
        return transport switch {
            ToolkitTransport.RockUsb => "rockusb",
            ToolkitTransport.LibUsb => "libusb",
            _ => "auto"
        };
    }

    private static string? FormatStorage(
        ToolkitStorage? storage) {
        return storage switch {
            ToolkitStorage.Emmc => "emmc",
            ToolkitStorage.SdCard => "sd-card",
            ToolkitStorage.SpiNor => "spi-nor",
            _ => null
        };
    }

    private static string? FormatResetMode(
        ToolkitResetMode? mode) {
        return mode switch {
            ToolkitResetMode.ResetMassStorage =>
                "reset-mass-storage",
            ToolkitResetMode.PowerOff => "power-off",
            ToolkitResetMode.ResetMaskRom =>
                "reset-mask-rom",
            ToolkitResetMode.Disconnect => "disconnect",
            _ => null
        };
    }

    private static void AddOptional(
        IList<string> arguments,
        string option,
        string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        arguments.Add(option);
        arguments.Add(value);
    }

    private static void AddRequired(
        IList<string> arguments,
        string? value,
        string description) {
        arguments.Add(
            value ?? throw new ArgumentException(
                $"The toolkit request contains no {description}."));
    }
}
