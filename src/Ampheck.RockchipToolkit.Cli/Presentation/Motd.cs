namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal static class Motd {
    private const string ResourceName =
        "Ampheck.RockchipToolkit.Cli.MOTD.txt";
    private static readonly Lazy<string> Text = new(Read);

    public static void Write(TextWriter output) {
        output.WriteLine(Text.Value);
        output.WriteLine();
    }

    private static string Read() {
        using var stream = typeof(Motd).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd();
    }
}
