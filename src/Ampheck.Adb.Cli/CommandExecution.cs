namespace Ampheck.Adb.Cli;

internal static class CommandExecution {
    public static async Task<int> RunAsync(
        Func<Task> action) {
        try {
            await action().ConfigureAwait(false);
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine($"error: {error.Message}");
            return 1;
        }
    }

    public static async Task<int> RunAsync(
        Func<Task<int>> action) {
        try {
            return await action().ConfigureAwait(false);
        } catch (Exception error) {
            Console.Error.WriteLine($"error: {error.Message}");
            return 1;
        }
    }
}
