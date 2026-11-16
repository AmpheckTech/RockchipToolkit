namespace Ampheck.RockchipToolkit.Core.Operations;

public sealed record TaskProgress {
    public long Current { get; init; }
    public long Total { get; init; }
}
