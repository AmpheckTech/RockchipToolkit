using System.CommandLine;

namespace Ampheck.Adb.Cli.Commands;

internal static class ForwardCommands {
    public static void AddTo(
        RootCommand root,
        AdbCliOptions options) {
        var local = new Argument<string?>("local") {
            Arity = ArgumentArity.ZeroOrOne
        };
        var remote = new Argument<string?>("remote") {
            Arity = ArgumentArity.ZeroOrOne
        };
        var list = new Option<bool>("--list") {
            Description = "List active forwards."
        };
        var remove = new Option<string?>("--remove") {
            Description = "Remove a local tcp:PORT forward."
        };
        var removeAll = new Option<bool>("--remove-all") {
            Description = "Remove all forwards for the selected device."
        };
        var command = new Command(
            "forward",
            "Create, list or remove TCP port forwards.");
        command.Add(local);
        command.Add(remote);
        command.Add(list);
        command.Add(remove);
        command.Add(removeAll);
        command.SetAction(async (parseResult, cancellationToken) =>
            await CommandExecution.RunAsync(async () => {
                var settings = options.Read(parseResult);
                if (settings.Direct) {
                    throw new InvalidOperationException(
                        "Port forwarding requires an ADB server.");
                }

                var client = await AdbClientFactory.CreateAsync(
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (parseResult.GetValue(list)) {
                    Console.Out.Write(
                        await client.ListForwardsAsync(
                                cancellationToken)
                            .ConfigureAwait(false));
                    return;
                }

                var device = await DeviceSelection.RequireOnlineAsync(
                        client,
                        settings,
                        cancellationToken)
                    .ConfigureAwait(false);
                var removeValue = parseResult.GetValue(remove);
                if (removeValue is not null) {
                    await client.RemoveTcpForwardAsync(
                            device,
                            ParseTcpPort(removeValue, "local"),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (parseResult.GetValue(removeAll)) {
                    await client.RemoveAllForwardsAsync(
                            device,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                var localValue = parseResult.GetValue(local)
                    ?? throw new ArgumentException(
                        "A local tcp:PORT forward is required.");
                var remoteValue = parseResult.GetValue(remote)
                    ?? throw new ArgumentException(
                        "A remote tcp:PORT forward is required.");
                await client.CreateTcpForwardAsync(
                        device,
                        ParseTcpPort(localValue, "local"),
                        ParseTcpPort(remoteValue, "remote"),
                        cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false));
        root.Add(command);
    }

    private static int ParseTcpPort(
        string value,
        string role) {
        var separator = value.IndexOf(':');
        if (separator <= 0 ||
            !value[..separator].Equals(
                "tcp",
                StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(
                value[(separator + 1)..],
                out var port) ||
            port is < 0 or > ushort.MaxValue) {
            throw new ArgumentException(
                $"The {role} forward must use tcp:PORT.");
        }

        return port;
    }
}
