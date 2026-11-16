using System.Text;
using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Support.Serial;

internal sealed class SerialCommandRunner(
    SerialHubManager serial,
    ConsoleRenderer console
) {
    private static readonly TimeSpan ConnectTimeout =
        TimeSpan.FromSeconds(5);

    public Task<int> ListAsync() {
        foreach (var device in serial.ListDevices()) {
            Console.WriteLine(device.PortName);
        }

        return Task.FromResult(0);
    }

    public async Task<int> MonitorAsync(
        string portName,
        SerialPortOptions options,
        CancellationToken cancellationToken = default
    ) {
        await using var session = await serial.OpenSessionAsync(
                options)
            .ConfigureAwait(false);
        await session.WaitForConnectionAsync(
                ConnectTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var output = PumpOutputAsync(session, cancellationToken);
        await SerialTerminalRunner.RunAsync(
                (data, token) => session.WriteAsync(data, token),
                output,
                console,
                cancellationToken,
                "Serial monitor. Use Ctrl+] to leave.")
            .ConfigureAwait(false);
        return 0;
    }

    public async Task<int> SendAsync(
        string portName,
        string text,
        CancellationToken cancellationToken = default
    ) {
        await using var session = await serial.OpenSessionAsync(
                new SerialPortOptions { PortName = portName })
            .ConfigureAwait(false);
        await session.WaitForConnectionAsync(
                ConnectTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        await session.WriteAsync(
                Encoding.UTF8.GetBytes(text),
                cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    private static async Task PumpOutputAsync(
        ISerialSession session,
        CancellationToken cancellationToken
    ) {
        await foreach (var value in session.Output.ReadAllAsync(
            cancellationToken)) {
            await SerialTerminalRunner.WriteOutputAsync(
                    value,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }


}
