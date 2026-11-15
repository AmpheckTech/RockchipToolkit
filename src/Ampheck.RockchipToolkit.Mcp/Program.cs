using Ampheck.RockchipToolkit.Mcp;
using Ampheck.RockchipToolkit.Mcp.Tools;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(
    "Microsoft.AspNetCore",
    LogLevel.Warning);
builder.Logging.AddFilter(
    "ModelContextProtocol",
    LogLevel.Warning);
var bridgeOptions = McpBridgeOptions.FromConfiguration(
    builder.Configuration);
builder.WebHost.UseUrls(bridgeOptions.ListenUrl);
builder.Services.AddSingleton(bridgeOptions);
builder.Services.AddSingleton(_ => new ToolkitRpcClient(
    bridgeOptions.Endpoint,
    bridgeOptions.Token,
    "arkt-mcp",
    bridgeOptions.FileAccess));
builder.Services.AddSingleton<DeviceTools>();
builder.Services.AddSingleton<StorageTools>();
builder.Services.AddSingleton<SerialTools>();
builder.Services.AddSingleton<AdbTools>();
builder.Services.AddSingleton<TaskTools>();
builder.Services.AddSingleton<ServerTools>();
builder.Services
    .AddMcpServer(options => {
        options.ServerInfo = new Implementation {
            Name = "ampheck-rockchip-toolkit",
            Version = ToolkitVersion.Current
        };
    })
    .WithHttpTransport(options => {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<DeviceTools>(McpJson.ToolOptions)
    .WithTools<StorageTools>(McpJson.ToolOptions)
    .WithTools<SerialTools>(McpJson.ToolOptions)
    .WithTools<AdbTools>(McpJson.ToolOptions)
    .WithTools<TaskTools>(McpJson.ToolOptions)
    .WithTools<ServerTools>(McpJson.ToolOptions)
    .WithRequestFilters(filters =>
        filters.AddCallToolFilter(next =>
            async (request, cancellationToken) => {
                try {
                    return await next(
                        request,
                        cancellationToken);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception error) {
                    return new CallToolResult {
                        IsError = true,
                        Content = [
                            new TextContentBlock {
                                Text = error.Message
                            }
                        ]
                    };
                }
            }));
var app = builder.Build();
app.MapMcp("/mcp");
await app.RunAsync();
