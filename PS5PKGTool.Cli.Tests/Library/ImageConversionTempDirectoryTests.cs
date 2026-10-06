using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Tests.Library;

public class ImageConversionTempDirectoryTests
{
    [Fact]
    public async Task Image_conversion_extracts_under_the_given_temp_directory_and_leaves_it_empty()
    {
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string exfat = temp.Combine("game.exfat");
        await Ps5ImageConversionService.ConvertAsync(dump, exfat, Ps5ImageConversionTarget.Exfat);
        string scratch = temp.Combine("scratch");
        Directory.CreateDirectory(scratch);

        Ps5ImageConversionResult result = await Ps5ImageConversionService.ConvertAsync(exfat,
            temp.Combine("game.ffpkg"), Ps5ImageConversionTarget.Ffpkg, tempDirectory: scratch);

        Assert.True(File.Exists(result.OutputPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
    }

    [Fact]
    public async Task Image_conversion_fails_when_the_temp_directory_is_unusable()
    {
        // Proves the parameter is honoured: a file where the temp folder should be makes extraction fail.
        using var temp = new TempDir();
        string dump = TestDump.Create(temp.Path);
        string exfat = temp.Combine("game.exfat");
        await Ps5ImageConversionService.ConvertAsync(dump, exfat, Ps5ImageConversionTarget.Exfat);
        string blocker = temp.Combine("blocker");
        File.WriteAllText(blocker, "not a folder");

        await Assert.ThrowsAnyAsync<IOException>(() => Ps5ImageConversionService.ConvertAsync(exfat,
            temp.Combine("game.ffpkg"), Ps5ImageConversionTarget.Ffpkg, tempDirectory: blocker));

        Assert.False(File.Exists(temp.Combine("game.ffpkg")));
    }
}
