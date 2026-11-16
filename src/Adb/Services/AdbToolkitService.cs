using Ampheck.RockchipToolkit.Adb.Client;
using Ampheck.RockchipToolkit.Adb.Server;

namespace Ampheck.RockchipToolkit.Adb.Services;

internal sealed partial class AdbToolkitService {
    private readonly AdbServerProcess _server;
    private readonly AdbClient _client;

    public AdbToolkitService(
        AdbServerProcess server,
        AdbClient client
    ) {
        _server = server;
        _client = client;
    }

}
