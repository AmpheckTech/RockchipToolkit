using System.Net.Sockets;
using System.Text;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Http;
using Ampheck.RockchipToolkit.Server.Rpc;
using Ampheck.RockchipToolkit.Server.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

public sealed partial class ToolkitHost {
    private async Task HandleClientAsync(
        TcpClient client,
        CancellationToken serverToken) {
        Interlocked.Increment(ref _activeClients);
        client.NoDelay = true;
        var peer = client.Client.RemoteEndPoint?.ToString() ??
            "unknown";
        try {
            var stream = client.GetStream();
            var request = await HttpRequestHead.ReadAsync(
                    stream,
                    serverToken)
                .ConfigureAwait(false);
            if (request.Method.Equals(
                    "OPTIONS",
                    StringComparison.OrdinalIgnoreCase)) {
                await HttpResponses.WriteHeadAsync(
                        stream,
                        204,
                        "No Content",
                        "text/plain",
                        contentLength: 0,
                        cancellationToken: serverToken)
                    .ConfigureAwait(false);
                return;
            }

            if (!TokenMatches(request)) {
                await HttpResponses.WriteErrorAsync(
                        stream,
                        401,
                        "Unauthorized",
                        "Authentication failed.",
                        serverToken)
                    .ConfigureAwait(false);
                return;
            }

            if (request.Path.Equals(
                    "/api/rpc",
                    StringComparison.Ordinal)) {
                await HandleRpcAsync(
                        stream,
                        request,
                        peer,
                        serverToken)
                    .ConfigureAwait(false);
                return;
            }

            if (request.Path.StartsWith(
                    "/api/serial/",
                    StringComparison.Ordinal)) {
                await HandleSerialAsync(
                        stream,
                        request,
                        serverToken)
                    .ConfigureAwait(false);
                return;
            }

            if (request.Path.StartsWith(
                    "/api/adb/",
                    StringComparison.Ordinal)) {
                await HandleAdbAsync(
                        stream,
                        request,
                        serverToken)
                    .ConfigureAwait(false);
                return;
            }

            if (request.Path.StartsWith(
                    "/api/blobs/",
                    StringComparison.Ordinal)) {
                await HandleBlobAsync(
                        stream,
                        request,
                        serverToken)
                    .ConfigureAwait(false);
                return;
            }

            await HttpResponses.WriteErrorAsync(
                    stream,
                    404,
                    "Not Found",
                    "The requested endpoint was not found.",
                    serverToken)
                .ConfigureAwait(false);
        } catch (EndOfStreamException) {
        } catch (Exception error) when (
            error is IOException or
            SocketException or
            OperationCanceledException or
            InvalidDataException or
            System.Net.WebSockets.WebSocketException) {
            _logger.LogDebug(
                "Toolkit client disconnected: {ErrorMessage}",
                error.Message);
        } catch (Exception error) {
            _logger.LogError(
                error,
                "Toolkit client handler failed");
        } finally {
            client.Dispose();
            Interlocked.Decrement(ref _activeClients);
            Interlocked.Exchange(
                ref _lastActivityTimestamp,
                DateTimeOffset.UtcNow.UtcTicks);
        }
    }

    private async Task HandleRpcAsync(
        Stream stream,
        HttpRequestHead request,
        string peer,
        CancellationToken serverToken) {
        using var socket = await WebSocketUpgrade.AcceptAsync(
                stream,
                request,
                RpcProtocol,
                serverToken)
            .ConfigureAwait(false);
        var connectionId = Interlocked.Increment(
            ref _nextClientId);
        var client = new ClientInfo(
            NormalizeClientName(
                request.Header("X-ARKT-Client")),
            peer,
            connectionId.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        await using var connection = new JsonRpcConnection(
            socket,
            _services.GetRequiredService<IServiceScopeFactory>(),
            _tasks,
            client,
            connectionId,
            _loggerFactory.CreateLogger<JsonRpcConnection>(),
            serverToken);
        await connection.RunAsync().ConfigureAwait(false);
    }

    private async Task HandleSerialAsync(
        Stream stream,
        HttpRequestHead request,
        CancellationToken serverToken) {
        using var socket = await WebSocketUpgrade.AcceptAsync(
                stream,
                request,
                SerialProtocol,
                serverToken)
            .ConfigureAwait(false);
        var sessionId = request.Path["/api/serial/".Length..];
        var lease = await _serialTerminals.TakeAsync(
                sessionId,
                serverToken)
            .ConfigureAwait(false);
        await ToolkitStreams.RunSerialAsync(
                socket,
                lease,
                serverToken)
            .ConfigureAwait(false);
    }

    private async Task HandleAdbAsync(
        Stream stream,
        HttpRequestHead request,
        CancellationToken serverToken) {
        using var socket = await WebSocketUpgrade.AcceptAsync(
                stream,
                request,
                AdbProtocol,
                serverToken)
            .ConfigureAwait(false);
        var sessionId = request.Path["/api/adb/".Length..];
        var shell = await _adb.TakeShellAsync(
                sessionId,
                serverToken)
            .ConfigureAwait(false);
        await ToolkitStreams.RunAdbShellAsync(
                socket,
                shell,
                serverToken)
            .ConfigureAwait(false);
    }

    private bool TokenMatches(HttpRequestHead request) {
        if (string.IsNullOrEmpty(_options.Token)) {
            return true;
        }

        var token = request.Header("Authorization");
        if (token?.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase) == true) {
            token = token["Bearer ".Length..].Trim();
        } else {
            token = request.Query("token");
        }

        if (string.IsNullOrEmpty(token)) {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(_options.Token);
        var actual = Encoding.UTF8.GetBytes(token);
        return expected.Length == actual.Length &&
            System.Security.Cryptography.CryptographicOperations
                .FixedTimeEquals(expected, actual);
    }

    private static string NormalizeClientName(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return Environment.MachineName;
        }

        var name = value.Trim();
        return name.Length > 128 ||
            name.Any(char.IsControl)
                ? Environment.MachineName
                : name;
    }
}
