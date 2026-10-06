using PS5PKGTool.Core.Builders;

namespace PS5PKGTool.Cli.Tests.Commands;

public class PackageConversionTests
{
    private static async Task<string> BuildPackageAsync(TempDir temp)
    {
        string dump = TestDump.Create(temp.Path);
        string package = temp.Combine("game.pkg");
        // FakeSignModules=false: the fixture's eboot.bin is random bytes, not an ELF.
        await ProsperoDebugPackageBuilder.CreateFromDirectoryAsync(dump, package,
            new ProsperoDebugPackageBuildOptions { ContentId = TestDump.ContentId, FakeSignModules = false });
        return package;
    }

    [Fact]
    public async Task Info_reads_a_debug_package()
    {
        using var temp = new TempDir();
        string package = await BuildPackageAsync(temp);

        CliResult result = await CliRunner.RunAsync("info", package);

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Contains(("Title ID:").PadRight(19) + TestDump.TitleId + "\n", result.Out);
        Assert.Contains(("Format:").PadRight(19) + "Debug PKG\n", result.Out);
    }

    [Fact]
    public async Task Debug_package_converts_to_an_image_through_the_temp_folder()
    {
        using var temp = new TempDir();
        string package = await BuildPackageAsync(temp);
        string scratch = temp.Combine("scratch");
        Directory.CreateDirectory(scratch);
        string output = temp.Combine("from-pkg.exfat");

        CliResult convert = await CliRunner.RunAsync("convert", package, "-o", output, "--temp", scratch, "--quiet");

        Assert.True(convert.ExitCode == 0, convert.Error);
        Assert.Contains("note: extracting to " + scratch + "\n", convert.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
        CliResult info = await CliRunner.RunAsync("info", output);
        Assert.Contains(("Title ID:").PadRight(19) + TestDump.TitleId + "\n", info.Out);
    }
}
