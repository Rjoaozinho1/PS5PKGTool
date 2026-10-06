using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PS5PKGTool.Cli.Tests.Commands;

/// <summary>
/// Runs the real ps5pkg executable as a child process, signals it mid-conversion, and checks that it
/// cancels cleanly: exit code 130, no output, no *.tmp next to it, and nothing left in its temp folder.
/// </summary>
public class SignalCancellationTests
{
    private const int SigHup = 1;
    private const int SigInt = 2;
    private const int SigTerm = 15;

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);

    [Theory]
    [InlineData(SigInt)]  // Ctrl+C
    [InlineData(SigTerm)] // kill, timeout, systemctl stop
    [InlineData(SigHup)]  // closed terminal or SSH session
    public async Task A_signal_mid_conversion_cancels_and_leaves_nothing_behind(int signal)
    {
        if (OperatingSystem.IsWindows()) return; // POSIX signals only

        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        var big = new byte[128 * 1024 * 1024]; // incompressible, so level-9 compression takes seconds
        new Random(3).NextBytes(big);
        File.WriteAllBytes(Path.Combine(dump, "data", "big.bin"), big);
        string output = temp.Combine("game.ffpfsc");
        string childTemp = temp.Combine("child-tmp");
        Directory.CreateDirectory(childTemp);

        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (string arg in new[] { typeof(CliApp).Assembly.Location, "convert", dump, "-o", output, "--level", "9" })
            start.ArgumentList.Add(arg);
        start.Environment["TMPDIR"] = childTemp;
        start.Environment["DOTNET_EnableDiagnostics"] = "0"; // no runtime IPC sockets in TMPDIR

        using Process child = Process.Start(start)!;
        var firstStage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        child.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) firstStage.TrySetResult(); };
        child.BeginErrorReadLine();
        child.BeginOutputReadLine();

        await firstStage.Task.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(0, kill(child.Id, signal));
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(130, child.ExitCode);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(childTemp));
    }
}
