using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitRequestFactory {
    public static ToolkitRequest Create(
        string command,
        CommandOptions options,
        bool verbose) {
        return new ToolkitRequest {
            Operation = ParseOperation(command),
            Transport = options.Transport switch {
                TransportKind.RockUsb =>
                    ToolkitTransport.RockUsb,
                TransportKind.LibUsb =>
                    ToolkitTransport.LibUsb,
                _ => ToolkitTransport.Auto
            },
            VendorId = options.VendorId,
            ProductId = options.ProductId,
            Loader = options.Loader,
            SourceLocation = ResolveInputKind(options.Source),
            FileLocation = ResolveInputKind(options.File),
            LoaderLocation = ResolveInputKind(options.Loader),
            Source = options.Source,
            File = options.File,
            Backup = options.Backup,
            SerialPort = options.SerialPort,
            BaudRate = options.SerialBaudRate,
            SerialDataBits = options.SerialDataBits,
            SerialParity = options.SerialParity,
            SerialStopBits = options.SerialStopBits,
            SerialHandshake = options.SerialHandshake,
            SerialDtrEnable = options.SerialDtrEnable,
            SerialRtsEnable = options.SerialRtsEnable,
            SerialHistoryBytes = options.SerialHistoryBytes,
            Sector = options.Sector,
            WriteLength = options.WriteLength,
            WriteSourceOffset = options.WriteSourceOffset,
            Count = options.Count,
            ResetMode = options.Reset,
            Storage = options.Storage,
            EraseBeforeWrite = options.EraseBeforeWrite,
            ResetAfterFlash = options.ResetAfterFlash,
            Verbose = verbose
        };
    }

    private static ToolkitOperation ParseOperation(
        string command) {
        return command switch {
            CommandNames.Devices => ToolkitOperation.Devices,
            CommandNames.Info => ToolkitOperation.Info,
            CommandNames.ChipInfo => ToolkitOperation.ChipInfo,
            CommandNames.ChipId => ToolkitOperation.ChipId,
            CommandNames.Capability =>
                ToolkitOperation.Capability,
            CommandNames.Loader => ToolkitOperation.Loader,
            CommandNames.Flash => ToolkitOperation.Flash,
            CommandNames.Read => ToolkitOperation.Read,
            CommandNames.Write => ToolkitOperation.Write,
            CommandNames.Erase => ToolkitOperation.Erase,
            CommandNames.Storage => ToolkitOperation.Storage,
            CommandNames.Reset => ToolkitOperation.Reset,
            CommandNames.SerialList =>
                ToolkitOperation.SerialList,
            CommandNames.SerialOpen =>
                ToolkitOperation.SerialOpen,
            CommandNames.SerialClose =>
                ToolkitOperation.SerialClose,
            CommandNames.SerialTerminal =>
                ToolkitOperation.SerialTerminal,
            CommandNames.SerialHistory =>
                ToolkitOperation.SerialHistory,
            _ => throw new ArgumentException(
                $"Command '{command}' is not supported by the toolkit server.")
        };
    }

    private static ToolkitInputKind ResolveInputKind(
        string? source) {
        return source is not null &&
            Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https"
                ? ToolkitInputKind.Url
                : ToolkitInputKind.LocalPath;
    }
}
