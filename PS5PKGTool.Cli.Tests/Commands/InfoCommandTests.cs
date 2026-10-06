using System.Text.Json;
using PS5PKGTool.Cli.Output;
using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Tests.Commands;

public class InfoCommandTests
{
    private static string Line(string key, string value) => (key + ":").PadRight(19) + value + "\n";

    [Fact]
    public async Task Shows_the_metadata_of_a_dump()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);

        CliResult result = await CliRunner.RunAsync("info", dump);

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Contains(Line("Title", TestDump.Title), result.Out);
        Assert.Contains(Line("Title ID", TestDump.TitleId), result.Out);
        Assert.Contains(Line("Content ID", TestDump.ContentId), result.Out);
        Assert.Contains(Line("Version", "01.000.000"), result.Out);
        Assert.Contains(Line("Required firmware", "4.00"), result.Out);
        Assert.Contains(Line("DRM type", "standard"), result.Out);
        Assert.Contains(Line("Format", "Dump Files"), result.Out);
        Assert.Contains(Line("Path", dump), result.Out);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task Dump_size_is_the_total_of_its_files()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        long expected = Directory.EnumerateFiles(dump, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);

        CliResult result = await CliRunner.RunAsync("info", dump);

        Assert.Contains(Line("Size", SizeFormat.Bytes(expected)), result.Out);
    }

    [Fact]
    public async Task Json_prints_the_full_record()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);

        CliResult result = await CliRunner.RunAsync("info", dump, "--json");

        Assert.True(result.ExitCode == 0, result.Error);
        using JsonDocument document = JsonDocument.Parse(result.Out);
        Assert.Equal(TestDump.TitleId, document.RootElement.GetProperty("titleId").GetString());
        Assert.Equal("LooseDump", document.RootElement.GetProperty("sourceKind").GetString());
    }

    [Fact]
    public async Task Data_warnings_go_to_stderr()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path, paramJson: $$"""
            { "titleId": "{{TestDump.TitleId}}", "contentId": "{{TestDump.ContentId}}",
              "localizedParameters": { "en-US": { "titleName": "{{TestDump.Title}}" } } }
            """);

        CliResult result = await CliRunner.RunAsync("info", dump);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("warning: Default language is missing", result.Error);
        Assert.DoesNotContain("warning:", result.Out);
    }

    [Fact]
    public async Task Missing_path_argument_is_a_usage_error()
    {
        CliResult result = await CliRunner.RunAsync("info");

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: missing <path>\n", result.Error);
    }

    [Fact]
    public async Task Nonexistent_path_fails()
    {
        using var temp = new TempDir();
        string missing = temp.Combine("nope");

        CliResult result = await CliRunner.RunAsync("info", missing);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: no such file or directory: {missing}\n", result.Error);
    }

    [Fact]
    public async Task Folder_without_titles_fails()
    {
        using var temp = new TempDir();

        CliResult result = await CliRunner.RunAsync("info", temp.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: no PS5 title found at {temp.Path}\n", result.Error);
    }

    [Fact]
    public async Task Folder_with_several_titles_points_to_scan()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        await Ps5ImageConversionService.ConvertAsync(dump, temp.Combine("a.exfat"), Ps5ImageConversionTarget.Exfat);
        await Ps5ImageConversionService.ConvertAsync(dump, temp.Combine("b.exfat"), Ps5ImageConversionTarget.Exfat);

        CliResult result = await CliRunner.RunAsync("info", temp.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: found 2 titles at {temp.Path}; use 'ps5pkg scan'\n", result.Error);
    }
}
