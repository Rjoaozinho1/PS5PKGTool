using System.Text.Json;

namespace PS5PKGTool.Cli.Tests.Commands;

public class ScanCommandTests
{
    [Fact]
    public async Task Lists_dumps_in_subfolders_as_a_table()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path);

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--quiet");

        Assert.True(result.ExitCode == 0, result.Error);
        string[] lines = result.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Title ID  ", lines[0]);
        Assert.StartsWith(TestDump.TitleId + "  ", lines[1]);
        Assert.Contains(TestDump.Title, lines[1]);
    }

    [Fact]
    public async Task Json_prints_an_array()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path);

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--json", "--quiet");

        using JsonDocument document = JsonDocument.Parse(result.Out);
        Assert.Equal(1, document.RootElement.GetArrayLength());
        Assert.Equal(TestDump.TitleId, document.RootElement[0].GetProperty("titleId").GetString());
    }

    [Fact]
    public async Task Dump_size_is_the_total_of_its_files()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        long expected = Directory.EnumerateFiles(dump, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--json", "--quiet");

        using JsonDocument document = JsonDocument.Parse(result.Out);
        Assert.Equal(expected, document.RootElement[0].GetProperty("sourceSize").GetInt64());
    }

    [Fact]
    public async Task No_recurse_only_looks_at_the_top_level()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path); // the dump is one level down

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--no-recurse", "--json", "--quiet");

        using JsonDocument document = JsonDocument.Parse(result.Out);
        Assert.Equal(0, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Empty_folder_reports_no_titles_on_stderr()
    {
        using var temp = new TempDir();

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--quiet");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Out);
        Assert.Equal("no PS5 titles found\n", result.Error);
    }

    [Fact]
    public async Task Unreadable_sources_are_warnings_and_exit_1_but_other_titles_are_listed()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path);
        TestDump.Create(temp.Path, "broken-app", paramJson: "{ not json");

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--quiet");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("warning: ", result.Error);
        Assert.Contains("broken-app", result.Error);
        Assert.Contains(TestDump.TitleId, result.Out);
    }

    [Fact]
    public async Task Titles_with_control_characters_or_long_names_stay_on_one_line()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path, paramJson: TestDump.ParamJson(
            @"Line one\nLine two that is far longer than forty characters in total"));

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--quiet");

        string[] lines = result.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("Line one Line two that is far longer th…", lines[1]);
    }

    [Fact]
    public async Task Paths_with_control_characters_stay_on_one_line()
    {
        using var temp = new TempDir();
        TestDump.Create(temp.Path, "odd\nname\u001b[31m");

        CliResult result = await CliRunner.RunAsync("scan", temp.Path, "--quiet");

        string[] lines = result.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain('\u001b', result.Out);
    }

    [Fact]
    public async Task Missing_folder_fails()
    {
        using var temp = new TempDir();
        string missing = temp.Combine("nope");

        CliResult result = await CliRunner.RunAsync("scan", missing);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal($"error: no such file or directory: {missing}\n", result.Error);
    }

    [Fact]
    public async Task Missing_folder_argument_is_a_usage_error()
    {
        CliResult result = await CliRunner.RunAsync("scan");

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("error: missing <folder>\n", result.Error);
    }
}
