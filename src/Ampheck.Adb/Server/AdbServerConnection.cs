using System.Net.Sockets;

namespace Ampheck.Adb.Server;

internal sealed class AdbServerConnection : IDisposable {
    public AdbServerConnection(
        TcpClient client,
        NetworkStream stream) {
        Client = client;
        Stream = stream;
    }

    public TcpClient Client { get; }

    public NetworkStream Stream { get; }

    public void Dispose() {
        Client.Dispose();
    }
}
