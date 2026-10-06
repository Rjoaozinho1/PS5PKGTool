using System.Text.Json;

namespace PS5PKGTool.Cli.Tests.Commands;

public class ConvertCommandTests
{
    private static async Task<string> MakeExfatAsync(TempDir temp, string dump)
    {
        string exfat = temp.Combine("game.exfat");
        CliResult result = await CliRunner.RunAsync("convert", dump, "-o", exfat, "--quiet");
        Assert.True(result.ExitCode == 0, result.Error);
        return exfat;
    }

    [Theory]
    [InlineData("game.exfat", "exFAT")]
    [InlineData("game.ffpkg", "FFPKG")]
    [InlineData("game.ffpfsc", "FFPFSC")]
    public async Task Dump_converts_to_each_format_and_the_image_reads_back(string fileName, string format)
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string output = temp.Combine(fileName);

        CliResult convert = await CliRunner.RunAsync("convert", dump, "-o", output, "--quiet");

        Assert.True(convert.ExitCode == 0, convert.Error);
        Assert.StartsWith(output + "  ", convert.Out);
        Assert.DoesNotContain("note: extracting", convert.Error);

        CliResult info = await CliRunner.RunAsync("info", output);
        Assert.True(info.ExitCode == 0, info.Error);
        Assert.Contains(("Title ID:").PadRight(19) + TestDump.TitleId + "\n", info.Out);
        Assert.Contains(("Format:").PadRight(19) + format + "\n", info.Out);
    }

    [Fact]
    public async Task Image_converts_to_another_image_through_the_temp_folder()
    {
        using var temp = new TempDir();
        string exfat = await MakeExfatAsync(temp, TestDump.Create(temp.Path));
        string scratch = temp.Combine("scratch");
        Directory.CreateDirectory(scratch);

        CliResult result = await CliRunner.RunAsync("convert", exfat, "-o", temp.Combine("game.ffpkg"),
            "--temp", scratch, "--quiet");

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Contains("note: extracting to " + scratch + "\n", result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
    }

    [Fact]
    public async Task Image_wraps_into_ffpfsc_without_extracting()
    {
        using var temp = new TempDir();
        string exfat = await MakeExfatAsync(temp, TestDump.Create(temp.Path));

        CliResult result = await CliRunner.RunAsync("convert", exfat, "-o", temp.Combine("game.ffpfsc"), "--quiet");

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.DoesNotContain("note: extracting", result.Error);
        Assert.EndsWith("(1 file)\n", result.Out);
    }

    [Fact]
    public async Task Scan_finds_the_dump_and_every_converted_image()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        foreach (string name in new[] { "game.exfat", "game.ffpkg", "game.ffpfsc" })
            Assert.Equal(0, (await CliRunner.RunAsync("convert", dump, "-o", temp.Combine(name), "--quiet")).ExitCode);

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--json", "--quiet");

        Assert.True(result.ExitCode == 0, result.Error);
        using JsonDocument document = JsonDocument.Parse(result.Out);
        Assert.Equal(4, document.RootElement.GetArrayLength());
        Assert.All(document.RootElement.EnumerateArray(),
            game => Assert.Equal(TestDump.TitleId, game.GetProperty("titleId").GetString()));
    }

    [Fact]
    public async Task Existing_output_fails_before_any_work_unless_forced()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string output = temp.Combine("game.exfat");
        File.WriteAllText(output, "keep me");

        // No --quiet: if any build stage had started, its progress line would appear before the error.
        CliResult refused = await CliRunner.RunAsync("convert", dump, "-o", output);

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal($"error: the output already exists: {output} (use --force to overwrite)\n", refused.Error);
        Assert.Equal("keep me", File.ReadAllText(output));

        CliResult forced = await CliRunner.RunAsync("convert", dump, "-o", output, "--force", "--quiet");

        Assert.True(forced.ExitCode == 0, forced.Error);
        Assert.NotEqual(7, new FileInfo(output).Length);
    }

    [Fact]
    public async Task Output_inside_the_source_dump_is_refused()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string output = Path.Combine(dump, "inside.exfat");

        CliResult result = await CliRunner.RunAsync("convert", dump, "-o", output);

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: the output must not be inside the source dump folder\n", result.Error);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Output_inside_the_dump_through_a_symlink_is_refused()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string link = temp.Combine("linkdump");
        Directory.CreateSymbolicLink(link, dump);

        CliResult result = await CliRunner.RunAsync("convert", dump, "-o", Path.Combine(link, "game.ffpkg"));

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: the output must not be inside the source dump folder\n", result.Error);
        Assert.False(File.Exists(Path.Combine(dump, "game.ffpkg")));
    }

    [Fact]
    public async Task Dump_given_through_a_symlink_still_refuses_an_output_inside_it()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string link = temp.Combine("linkdump");
        Directory.CreateSymbolicLink(link, dump);

        CliResult result = await CliRunner.RunAsync("convert", link, "-o", Path.Combine(dump, "game.ffpkg"));

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: the output must not be inside the source dump folder\n", result.Error);
        Assert.False(File.Exists(Path.Combine(dump, "game.ffpkg")));
    }

    [Fact]
    public async Task Folder_that_is_not_a_dump_is_refused()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path); // temp.Path is now a "library" folder containing a dump
        string output = temp.Combine("library.exfat");

        CliResult result = await CliRunner.RunAsync("convert", temp.Path, "-o", output);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: {temp.Path} is not a PS5 dump folder (no sce_sys/param.json)\n", result.Error);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Unknown_source_file_is_refused()
    {
        using var temp = new TempDir();
        string notes = temp.Combine("notes.txt");
        File.WriteAllText(notes, "hello");

        CliResult result = await CliRunner.RunAsync("convert", notes, "-o", temp.Combine("x.exfat"));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: {notes} is not a PS5 dump folder, debug .pkg, or exFAT/FFPKG/FFPFSC image\n", result.Error);
    }

    [Fact]
    public async Task Same_format_conversion_is_refused()
    {
        using var temp = new TempDir();
        string exfat = await MakeExfatAsync(temp, TestDump.Create(temp.Path));

        CliResult result = await CliRunner.RunAsync("convert", exfat, "-o", temp.Combine("copy.exfat"));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("error: converting this exFAT image to exfat is not supported\n", result.Error);
    }

    [Fact]
    public async Task Cluster_options_are_refused_when_wrapping_an_image()
    {
        using var temp = new TempDir();
        string exfat = await MakeExfatAsync(temp, TestDump.Create(temp.Path));

        CliResult result = await CliRunner.RunAsync("convert", exfat, "-o", temp.Combine("game.ffpfsc"), "--cluster", "32k");

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: --cluster and --no-ampr have no effect when wrapping", result.Error);
    }

    [Theory]
    [InlineData("missing-source")]
    [InlineData("missing-output-folder")]
    [InlineData("missing-temp")]
    [InlineData("output-is-folder")]
    public async Task Path_problems_fail_with_exit_1(string scenario)
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        (string[] args, string expected) = scenario switch
        {
            "missing-source" => (new[] { "convert", temp.Combine("nope"), "-o", temp.Combine("x.exfat") },
                $"error: no such file or directory: {temp.Combine("nope")}\n"),
            "missing-output-folder" => (new[] { "convert", dump, "-o", temp.Combine("no", "x.exfat") },
                $"error: the output folder does not exist: {temp.Combine("no")}\n"),
            "missing-temp" => (new[] { "convert", dump, "-o", temp.Combine("x.exfat"), "--temp", temp.Combine("nope") },
                $"error: the temp folder does not exist: {temp.Combine("nope")}\n"),
            _ => (new[] { "convert", dump, "-o", temp.Path, "--to", "exfat" },
                $"error: the output path is a folder: {temp.Path}\n"),
        };

        CliResult result = await CliRunner.RunAsync(args);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData(new[] { "convert", "-o", "x.exfat" }, "error: missing <source>")]
    [InlineData(new[] { "convert", "dump" }, "error: missing -o/--output <path>")]
    [InlineData(new[] { "convert", "dump", "-o", "x.img" }, "error: cannot tell the target format from the output name")]
    [InlineData(new[] { "convert", "dump", "-o", "x.exfat", "--level", "5" }, "error: --level only applies to --to ffpfsc")]
    public async Task Usage_errors_exit_2(string[] args, string expected)
    {
        CliResult result = await CliRunner.RunAsync(args);

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith(expected, result.Error);
    }

    [Fact]
    public async Task Mismatched_to_and_extension_warns_and_succeeds()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);

        CliResult result = await CliRunner.RunAsync("convert", dump, "-o", temp.Combine("game.exfat"), "--to", "ffpkg", "--quiet");

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Contains("warning: --to ffpkg does not match the output extension '.exfat'; writing ffpkg\n", result.Error);
    }

    [Fact]
    public async Task Cancelled_run_exits_130_and_leaves_nothing_behind()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string output = temp.Combine("game.exfat");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        CliResult result = await CliRunner.RunAsync(null, cancellation.Token, "convert", dump, "-o", output);

        Assert.Equal(130, result.ExitCode);
        Assert.Equal("cancelled\n", result.Error);
        Assert.False(File.Exists(output));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Help_lists_the_format_options()
    {
        CliResult result = await CliRunner.RunAsync("convert", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--inode-density 256k|512k|1m", result.Out);
        Assert.Contains("-o, --output <path>", result.Out);
    }
}
