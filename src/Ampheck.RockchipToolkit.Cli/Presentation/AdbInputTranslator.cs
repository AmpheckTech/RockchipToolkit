namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal static class AdbInputTranslator {
    public static string Translate(ConsoleKeyInfo key) {
        if (key.Key == ConsoleKey.Enter) {
            return "\r";
        }

        if (key.Key == ConsoleKey.Backspace) {
            return "\x7f";
        }

        return key.Key switch {
            ConsoleKey.Tab => "\t",
            ConsoleKey.Escape => "\x1b",
            ConsoleKey.UpArrow => "\x1b[A",
            ConsoleKey.DownArrow => "\x1b[B",
            ConsoleKey.RightArrow => "\x1b[C",
            ConsoleKey.LeftArrow => "\x1b[D",
            ConsoleKey.Home => "\x1b[H",
            ConsoleKey.End => "\x1b[F",
            ConsoleKey.Delete => "\x1b[3~",
            ConsoleKey.PageUp => "\x1b[5~",
            ConsoleKey.PageDown => "\x1b[6~",
            _ when key.Key == ConsoleKey.C &&
                (key.Modifiers & ConsoleModifiers.Control) != 0 =>
                "\x03",
            _ when key.Key == ConsoleKey.D &&
                (key.Modifiers & ConsoleModifiers.Control) != 0 =>
                "\x04",
            _ when key.KeyChar != '\0' => key.KeyChar.ToString(),
            _ => string.Empty
        };
    }
}
