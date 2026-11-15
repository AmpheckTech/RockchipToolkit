using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record SerialOpenArguments(
    string PortName,
    SerialPortSettings Settings);

internal sealed record SerialPortArguments(string PortName);

internal sealed record SerialHistoryArguments(
    string PortName,
    int MaximumBytes);

internal sealed record SerialTerminalArguments(
    string PortName,
    int HistoryBytes);
