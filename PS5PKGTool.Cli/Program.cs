namespace PS5PKGTool.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        int presses = 0;
        Console.CancelKeyPress += (_, e) =>
        {
            // The first Ctrl+C cancels cleanly (the library removes its temp files); a second one ends the process.
            if (Interlocked.Increment(ref presses) > 1) return;
            e.Cancel = true;
            cancellation.Cancel();
        };
        return await CliApp.RunAsync(args, Console.Out, Console.Error, !Console.IsErrorRedirected, cancellation.Token)
            .ConfigureAwait(false);
    }
}
