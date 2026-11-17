namespace Ampheck.RockchipToolkit.Images.Rkfw;

internal static class RockchipImageKindExtensions {
    public static bool IsNewIdb(this RockchipImageKind kind) {
        return kind is RockchipImageKind.Rkns or RockchipImageKind.Rkss;
    }

    public static bool IsStandaloneBootImage(this RockchipImageKind kind) {
        return kind is
            RockchipImageKind.Boot or
            RockchipImageKind.Rkns or
            RockchipImageKind.Rkss;
    }
}
