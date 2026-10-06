using System.Runtime.InteropServices;

namespace PS5PKGTool.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        int signals = 0;

        // Ctrl+C (SIGINT), kill/timeout/systemctl stop (SIGTERM) and a closed terminal or SSH session (SIGHUP)
        // all cancel cleanly, so the library removes its temp files; a second signal ends the process.
        void OnSignal(PosixSignalContext context)
        {
            if (Interlocked.Increment(ref signals) > 1) return;
            context.Cancel = true;
            cancellation.Cancel();
        }

        using PosixSignalRegistration sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnSignal);
        using PosixSignalRegistration sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);
        using PosixSignalRegistration sighup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnSignal);
        return await CliApp.RunAsync(args, Console.Out, Console.Error, !Console.IsErrorRedirected, cancellation.Token)
            .ConfigureAwait(false);
    }
}
