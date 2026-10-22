namespace Ampheck.RockchipToolkit.Rpc;

public static class ToolkitOperationNames {
    private static readonly IReadOnlyDictionary<ToolkitOperation, string> Names =
        new Dictionary<ToolkitOperation, string> {
            [ToolkitOperation.Devices] = "device.list",
            [ToolkitOperation.Info] = "device.info",
            [ToolkitOperation.ChipInfo] = "device.chip_info",
            [ToolkitOperation.ChipId] = "device.chip_id",
            [ToolkitOperation.Capability] = "device.capability",
            [ToolkitOperation.Loader] = "device.loader",
            [ToolkitOperation.Flash] = "flash.run",
            [ToolkitOperation.Read] = "storage.read",
            [ToolkitOperation.Write] = "storage.write",
            [ToolkitOperation.Erase] = "storage.erase",
            [ToolkitOperation.Storage] = "storage.select",
            [ToolkitOperation.Reset] = "device.reset",
            [ToolkitOperation.SerialList] = "serial.list",
            [ToolkitOperation.SerialOpen] = "serial.open",
            [ToolkitOperation.SerialClose] = "serial.close",
            [ToolkitOperation.SerialTerminal] = "serial.terminal",
            [ToolkitOperation.SerialHistory] = "serial.history"
        };

    private static readonly IReadOnlyDictionary<string, ToolkitOperation> Operations =
        Names.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static IReadOnlyList<string> All { get; } =
        Names.Values.Order(StringComparer.Ordinal).ToArray();

    public static string GetName(ToolkitOperation operation) =>
        Names.TryGetValue(operation, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(operation));

    public static bool TryParse(string method, out ToolkitOperation operation) =>
        Operations.TryGetValue(method, out operation);
}
