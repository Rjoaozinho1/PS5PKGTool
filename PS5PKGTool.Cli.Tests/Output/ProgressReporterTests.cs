using PS5PKGTool.Cli.Output;
using PS5PKGTool.Core.Services;
using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Tests.Output;

public class ProgressReporterTests
{
    private static StringWriter Writer() => new() { NewLine = "\n" };

    [Fact]
    public void Lines_mode_prints_each_stage_once()
    {
        StringWriter error = Writer();
        var progress = new ProgressReporter(error, ProgressMode.Lines);

        progress.Report(new Ps5ImageConversionProgress("Extracting", 1, 10));
        progress.Report(new Ps5ImageConversionProgress("Extracting", 5, 10));
        progress.Report(new Ps5ImageConversionProgress("Building image", 1, 10));
        progress.Finish();

        Assert.Equal("Extracting\nBuilding image\n", error.ToString());
    }

    [Fact]
    public void Terminal_mode_rewrites_one_line_and_throttles_redraws()
    {
        StringWriter error = Writer();
        long now = 0;
        var progress = new ProgressReporter(error, ProgressMode.Terminal, () => now);

        progress.Report(new Ps5ImageConversionProgress("Building image", 0, 2048));    // drawn: new stage
        now = 50;
        progress.Report(new Ps5ImageConversionProgress("Building image", 1024, 2048)); // skipped: < 100 ms
        now = 150;
        progress.Report(new Ps5ImageConversionProgress("Building image", 2048, 2048)); // drawn
        progress.Finish();

        Assert.Equal(
            "\rBuilding image  0%  (0 B / 2.0 KB)" +
            "\rBuilding image  100%  (2.0 KB / 2.0 KB)\n",
            error.ToString());
    }

    [Fact]
    public void Scan_progress_shows_counts()
    {
        StringWriter error = Writer();
        var progress = new ProgressReporter(error, ProgressMode.Terminal, () => 0);

        progress.Report(new Ps5ScanProgress { Processed = 1, Total = 4, CurrentPath = "/x" });
        progress.Finish();

        Assert.Equal("\rScanning  25%  (1 / 4)\n", error.ToString());
    }

    [Fact]
    public void Unknown_total_shows_only_the_stage() =>
        Assert.Equal("Preparing", ProgressReporter.Format("Preparing", 0, 0, sizes: true));

    [Fact]
    public void Off_mode_prints_nothing()
    {
        StringWriter error = Writer();
        var progress = new ProgressReporter(error, ProgressMode.Off);

        progress.Report(new Ps5ImageConversionProgress("Extracting", 1, 10));
        progress.Finish();

        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Reports_after_finish_are_ignored()
    {
        StringWriter error = Writer();
        var progress = new ProgressReporter(error, ProgressMode.Lines);

        progress.Finish();
        progress.Report(new Ps5ImageConversionProgress("Late", 1, 1));

        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Concurrent_reports_from_many_threads_do_not_throw()
    {
        StringWriter error = Writer();
        var progress = new ProgressReporter(error, ProgressMode.Terminal);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
                progress.Report(new Ps5ImageConversionProgress($"Stage {worker}", i, 1000));
        })));
        progress.Finish();

        Assert.EndsWith("\n", error.ToString());
    }
}
