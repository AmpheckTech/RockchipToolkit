using System.ComponentModel;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ampheck.RockchipToolkit.Mcp.Tools;

[McpServerToolType]
public sealed class SerialTools(ToolkitRpcClient client) {
    [McpServerTool(
        Name = "list_serial_ports",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description("List serial ports available on the toolkit server.")]
    public async Task<SerialListResult> ListPorts(
        CancellationToken cancellationToken = default) {
        return await client.RequestAsync<
            EmptyParams,
            SerialListResult>(
                JsonRpcMethods.SerialList,
                new EmptyParams(),
                cancellationToken: cancellationToken);
    }

    [McpServerTool(Name = "open_serial_port")]
    [Description("Open and keep a serial port open.")]
    public async Task<TextResult> OpenPort(
        [Description("Serial port name, such as /dev/ttyUSB0 or COM3.")]
        string portName,
        int baudRate = 150_000,
        int dataBits = 8,
        [Description("none, odd, even, mark, or space.")]
        string parity = "none",
        [Description("one, one-point-five, or two.")]
        string stopBits = "one",
        [Description("none, xon-xoff, rts-cts, or rts-cts-xon-xoff.")]
        string handshake = "none",
        bool dtrEnable = true,
        bool rtsEnable = true,
        CancellationToken cancellationToken = default) {
        await client.RequestAsync<SerialOpenParams, EmptyResult>(
            JsonRpcMethods.SerialOpen,
            new SerialOpenParams {
                PortName = portName,
                Settings = new SerialPortSettings {
                    BaudRate = baudRate,
                    DataBits = dataBits,
                    Parity = ParseParity(parity),
                    StopBits = ParseStopBits(stopBits),
                    Handshake = ParseHandshake(handshake),
                    DtrEnable = dtrEnable,
                    RtsEnable = rtsEnable
                }
            },
            cancellationToken: cancellationToken);
        return new TextResult {
            Text = $"Serial port {portName} opened."
        };
    }

    [McpServerTool(Name = "close_serial_port")]
    [Description("Close an open serial port.")]
    public async Task<TextResult> ClosePort(
        string portName,
        CancellationToken cancellationToken = default) {
        var result = await client.RequestAsync<
            SerialCloseParams,
            SerialCloseResult>(
                JsonRpcMethods.SerialClose,
                new SerialCloseParams { PortName = portName },
                cancellationToken: cancellationToken);
        return new TextResult {
            Text = result.Closed
                ? $"Serial port {portName} closed."
                : $"Serial port {portName} was not open."
        };
    }

    [McpServerTool(Name = "clear_serial_buffer")]
    [Description("Clear retained serial output.")]
    public async Task<TextResult> ClearBuffer(
        string portName,
        CancellationToken cancellationToken = default) {
        await client.RequestAsync<SerialPortParams, SerialClearResult>(
            JsonRpcMethods.SerialClear,
            new SerialPortParams { PortName = portName },
            cancellationToken: cancellationToken);
        return new TextResult {
            Text = $"Serial buffer cleared for {portName}."
        };
    }

    [McpServerTool(
        Name = "read_serial_history",
        UseStructuredContent = true)]
    [Description("Save retained serial output to a local file.")]
    public async Task<FileReference> ReadHistory(
        string portName,
        [Description("Local destination file or directory.")]
        string destinationPath,
        int maximumBytes = 256 * 1024,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) {
        var file = await client.RequestAsync<
            SerialHistoryParams,
            FileReference>(
                JsonRpcMethods.SerialHistory,
                new SerialHistoryParams {
                    PortName = portName,
                    MaximumBytes = maximumBytes,
                    OutputServerPath = client.UseServerPaths
                        ? Path.GetFullPath(destinationPath)
                        : null
                },
                cancellationToken: cancellationToken);
        try {
            return await ToolSupport.SaveOutputAsync(
                client,
                file,
                destinationPath,
                progress,
                cancellationToken);
        } finally {
            if (file.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId);
            }
        }
    }

    private static ToolkitSerialParity ParseParity(string value) {
        return value.ToLowerInvariant() switch {
            "none" => ToolkitSerialParity.None,
            "odd" => ToolkitSerialParity.Odd,
            "even" => ToolkitSerialParity.Even,
            "mark" => ToolkitSerialParity.Mark,
            "space" => ToolkitSerialParity.Space,
            _ => throw new ArgumentException(
                "Unknown serial parity.",
                nameof(value))
        };
    }

    private static ToolkitSerialStopBits ParseStopBits(string value) {
        return value.ToLowerInvariant() switch {
            "one" => ToolkitSerialStopBits.One,
            "one-point-five" => ToolkitSerialStopBits.OnePointFive,
            "two" => ToolkitSerialStopBits.Two,
            _ => throw new ArgumentException(
                "Unknown serial stop bits.",
                nameof(value))
        };
    }

    private static ToolkitSerialHandshake ParseHandshake(string value) {
        return value.ToLowerInvariant() switch {
            "none" => ToolkitSerialHandshake.None,
            "xon-xoff" => ToolkitSerialHandshake.XOnXOff,
            "rts-cts" => ToolkitSerialHandshake.RequestToSend,
            "rts-cts-xon-xoff" =>
                ToolkitSerialHandshake.RequestToSendXOnXOff,
            _ => throw new ArgumentException(
                "Unknown serial handshake.",
                nameof(value))
        };
    }
}
